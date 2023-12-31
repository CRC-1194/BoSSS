﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using BoSSS.Foundation.IO;
using ilPSP;
using NUnit.Framework;

namespace BoSSS.Application.DatabaseTests
{
    class DatabaseTest : MPITest
    {
        protected IDatabaseInfo databaseWithFiles;

        protected IDatabaseInfo emptyDatabase;
        
       
        
        [NUnitFileToCopyHack("DatabaseTests/bosss_db_test_template.zip")]
        [SetUp]
        public void Init()
        {
            string templateDatabasePath = "bosss_db_test_template.zip";

            // Temporary filesystem driver to unzip the test database.
            var tmpFsDriver = new StandardFsDriver(templateDatabasePath);

            databaseWithFiles = GetDatabaseCopy(Path.Combine(
                Path.GetTempPath(), "BoSSS_DB_" + Path.GetRandomFileName()),
                tmpFsDriver.BasePath);

            emptyDatabase = CreateEmptyDatabase(Path.Combine(
                Path.GetTempPath(), "BoSSS_DB_" + Path.GetRandomFileName()));
        }

        [TearDown]
        public void CleanUp()
        {
            RemoveTestDatabase(databaseWithFiles);
            RemoveTestDatabase(emptyDatabase);
        }

        private IDatabaseInfo CreateEmptyDatabase(string basePath)
        {
            if (Directory.Exists(basePath))
            {
                throw new Exception("Database folder already exists");
            }

            Directory.CreateDirectory(basePath);
            Directory.CreateDirectory(Path.Combine(basePath, StandardFsDriver.DistVectorDataDir));
            Directory.CreateDirectory(Path.Combine(basePath, StandardFsDriver.GridsDir));
            Directory.CreateDirectory(Path.Combine(basePath, StandardFsDriver.SessionsDir));
            Directory.CreateDirectory(Path.Combine(basePath, StandardFsDriver.TimestepDir));

            return DatabaseInfo.Open(basePath);
        }

        private IDatabaseInfo GetDatabaseCopy(string newBasePath, string templateBasePath)
        {
            CreateEmptyDatabase(newBasePath);
            IDatabaseInfo templateDB = DatabaseInfo.Open(templateBasePath);

            foreach (Guid sessionID in templateDB.Controller.DBDriver.FsDriver.GetAllSessionGUIDs())
            {
                Directory.CreateDirectory(Path.Combine(newBasePath,
                    StandardFsDriver.SessionsDir, sessionID.ToString()));
            }

            foreach (string filePath in Directory.GetFiles(templateBasePath, "*",
                SearchOption.AllDirectories))
            {
                File.Copy(filePath, filePath.Replace(templateBasePath, newBasePath));
            }

            return DatabaseInfo.Open(newBasePath);
        }

        private void RemoveTestDatabase(IDatabaseInfo database) {
            DatabaseInfo.Close(database);
            Directory.Delete(database.Path, true);
        }
    }
}
