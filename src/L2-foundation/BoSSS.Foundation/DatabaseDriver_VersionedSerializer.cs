﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MPI.Wrappers;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Bson;
using BoSSS.Foundation.Grid;
using ilPSP;

namespace BoSSS.Foundation.IO
{
    class UnbufferedStreamReader
    {
        Stream stream;
        long streamPositionBeforeReadLine;

        public UnbufferedStreamReader(Stream stream)
        {
            this.stream = stream;
        }

        public string ReadLine()
        {
            streamPositionBeforeReadLine = stream.Position;
            Queue<byte> line = new Queue<byte>();
            int current = ReadByte();
            while ( current != -1 && current != (int)'\n')
            {
                byte b = (byte)current;
                line.Enqueue(b);
                current = ReadByte();
                if(current == (int)'\r')
                {
                    current = ReadByte();
                }
            }
            byte[] lineArray = line.ToArray();
            return Encoding.ASCII.GetString(lineArray);
        }

        public int ReadByte()
        {
            return stream.ReadByte();
        }

        public void UndoLastReadLine()
        {
            stream.Position = streamPositionBeforeReadLine;
        }

    }

    class VersionManager : IVectorDataSerializer
    {
        IVectorDataSerializer preferedSerializer;
        IVectorDataSerializer standardSerializer;
        readonly Dictionary<string, IVectorDataSerializer> allSerializers;
        
        public VersionManager(
            IVectorDataSerializer prefered, 
            IVectorDataSerializer standard, 
            params IVectorDataSerializer[] additionalSerializers) 
        {
            this.preferedSerializer = prefered;
            this.standardSerializer = standard;
            allSerializers = new Dictionary<string, IVectorDataSerializer>(additionalSerializers.Length + 2);
            AddToAllSerializers(prefered);
            AddToAllSerializers(standard);  
            foreach(IVectorDataSerializer additionalSerializer in additionalSerializers)
            {
                AddToAllSerializers(additionalSerializer);
            }
        }

        void AddToAllSerializers( IVectorDataSerializer serializer)
        {
            if (!allSerializers.ContainsKey(serializer.Name))
            {
                allSerializers.Add(serializer.Name, serializer);
            }
        }

        IVectorDataSerializer GetDeserializer(Stream stream)
        {
            IVectorDataSerializer dataSerializer;
            var reader = new UnbufferedStreamReader(stream); //No Using, stream will be disposed elsewhere
            {
                string firstLine = reader.ReadLine();
                bool found = allSerializers.TryGetValue(firstLine, out dataSerializer);
                if (!found)
                {
                    reader.UndoLastReadLine();
                    dataSerializer = standardSerializer;
                }
            }
            return dataSerializer;
        }

        IVectorDataSerializer GetSerializer(Stream stream)
        {
            var writer = new StreamWriter(stream);
            {
                writer.WriteLine(preferedSerializer.Name);
                writer.Flush();
            }
            return preferedSerializer;
        }

        public object Deserialize(Stream stream, Type objectType)
        {
            object grid = GetDeserializer(stream).Deserialize(stream, objectType);
            return grid;
        }

        public void Serialize(Stream stream,object obj, Type objectType)
        {
            GetSerializer(stream).Serialize(stream, obj, objectType);
        }

        public Guid SaveVector<T>(IList<T> vector)
        {
            return standardSerializer.SaveVector(vector);
        }

        public void SaveVector<T>(IList<T> vector, Guid id)
        {
            standardSerializer.SaveVector(vector, id);
        }

        public IList<T> LoadVector<T>(Guid id, ref Partitioning part)
        {
            return standardSerializer.LoadVector<T>(id, ref part);
        }

        public string Name => throw new Exception("Does not have a Name");

    }

}
