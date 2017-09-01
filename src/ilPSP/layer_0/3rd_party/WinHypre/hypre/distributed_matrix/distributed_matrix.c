/*BHEADER**********************************************************************
 * Copyright (c) 2008,  Lawrence Livermore National Security, LLC.
 * Produced at the Lawrence Livermore National Laboratory.
 * This file is part of HYPRE.  See file COPYRIGHT for details.
 *
 * HYPRE is free software; you can redistribute it and/or modify it under the
 * terms of the GNU Lesser General Public License (as published by the Free
 * Software Foundation) version 2.1 dated February 1999.
 *
 * $Revision: 2.5 $
 ***********************************************************************EHEADER*/




/******************************************************************************
 *
 * Member functions for hypre_DistributedMatrix class.
 *
 *****************************************************************************/

#include "distributed_matrix.h"
#include "HYPRE.h"
#include <assert.h>

/*--------------------------------------------------------------------------
 *     BASIC CONSTRUCTION/DESTRUCTION SEQUENCE
 *--------------------------------------------------------------------------*/

/*--------------------------------------------------------------------------
 * hypre_DistributedMatrixCreate
 *--------------------------------------------------------------------------*/

hypre_DistributedMatrix *
hypre_DistributedMatrixCreate( MPI_Comm     context  )
{
   hypre_DistributedMatrix    *matrix;

   matrix = hypre_CTAlloc(hypre_DistributedMatrix, 1);

   hypre_DistributedMatrixContext(matrix) = context;
   hypre_DistributedMatrixM(matrix)    = -1;
   hypre_DistributedMatrixN(matrix)    = -1;
   hypre_DistributedMatrixAuxiliaryData(matrix)    = NULL;
   hypre_DistributedMatrixLocalStorage(matrix) = NULL;
   hypre_DistributedMatrixTranslator(matrix) = NULL;
   hypre_DistributedMatrixLocalStorageType(matrix) = HYPRE_UNITIALIZED;

#ifdef HYPRE_TIMING
   matrix->GetRow_timer = hypre_InitializeTiming( "GetRow" );
#endif

   return matrix;
}

/*--------------------------------------------------------------------------
 * hypre_DistributedMatrixDestroy
 *--------------------------------------------------------------------------*/

int 
hypre_DistributedMatrixDestroy( hypre_DistributedMatrix *matrix )
{

   if ( hypre_DistributedMatrixLocalStorageType(matrix) == HYPRE_PETSC )
//      hypre_DistributedMatrixDestroyPETSc( matrix );
      assert(0); // not implemented
   else if ( hypre_DistributedMatrixLocalStorageType(matrix) == HYPRE_ISIS )
//      hypre_FreeDistributedMatrixISIS( matrix );
      assert(0); // not implemented
   else if ( hypre_DistributedMatrixLocalStorageType(matrix) == HYPRE_PARCSR )
      hypre_DistributedMatrixDestroyParCSR( matrix );
   else
      return(-1);

#ifdef HYPRE_TIMING
   hypre_FinalizeTiming ( matrix->GetRow_timer );
#endif
   hypre_TFree(matrix);

   return(0);
}

/*--------------------------------------------------------------------------
 * hypre_DistributedMatrixLimitedDestroy
 *--------------------------------------------------------------------------*/

int 
hypre_DistributedMatrixLimitedDestroy( hypre_DistributedMatrix *matrix )
{

   hypre_TFree(matrix);

   return(0);
}


/*--------------------------------------------------------------------------
 * hypre_DistributedMatrixInitialize
 *--------------------------------------------------------------------------*/

int 
hypre_DistributedMatrixInitialize( hypre_DistributedMatrix *matrix )
{
   int ierr = 0;

   if ( hypre_DistributedMatrixLocalStorageType(matrix) == HYPRE_PETSC )
//    return( 0 );
      assert(0); // not implemented
   else if ( hypre_DistributedMatrixLocalStorageType(matrix) == HYPRE_ISIS )
//      ierr = hypre_InitializeDistributedMatrixISIS(matrix);
      assert(0); // not implemented
   else if ( hypre_DistributedMatrixLocalStorageType(matrix) == HYPRE_PARCSR )
      ierr = hypre_DistributedMatrixInitializeParCSR(matrix);
   else
      ierr = -1;

   return( ierr );
}

/*--------------------------------------------------------------------------
 * hypre_DistributedMatrixAssemble
 *--------------------------------------------------------------------------*/

int 
hypre_DistributedMatrixAssemble( hypre_DistributedMatrix *matrix )
{

   if( 
       (hypre_DistributedMatrixLocalStorageType(matrix) != HYPRE_PETSC )
    && (hypre_DistributedMatrixLocalStorageType(matrix) != HYPRE_ISIS )
    && (hypre_DistributedMatrixLocalStorageType(matrix) != HYPRE_PARCSR )
     )
     return(-1);


   if( hypre_DistributedMatrixLocalStorage(matrix) == NULL )
     return(-1);

   if( (hypre_DistributedMatrixM(matrix) < 0 ) ||
       (hypre_DistributedMatrixN(matrix) < 0 ) )
     return(-1);

   return(0);
}

/*--------------------------------------------------------------------------
 *     Get/Sets that are independent of underlying storage type
 *--------------------------------------------------------------------------*/

/*--------------------------------------------------------------------------
 * hypre_DistributedMatrixSetLocalStorageType
 *--------------------------------------------------------------------------*/

int 
hypre_DistributedMatrixSetLocalStorageType( hypre_DistributedMatrix *matrix,
				 int                type   )
{
   int ierr=0;

   hypre_DistributedMatrixLocalStorageType(matrix) = type;

   return(ierr);
}

/*--------------------------------------------------------------------------
 * hypre_DistributedMatrixGetLocalStorageType
 *--------------------------------------------------------------------------*/

int 
hypre_DistributedMatrixGetLocalStorageType( hypre_DistributedMatrix *matrix  )
{
   int ierr=0;

   ierr = hypre_DistributedMatrixLocalStorageType(matrix);

   return(ierr);
}

/*--------------------------------------------------------------------------
 * hypre_DistributedMatrixSetLocalStorage
 *--------------------------------------------------------------------------*/

int 
hypre_DistributedMatrixSetLocalStorage( hypre_DistributedMatrix *matrix,
				 void                  *local_storage  )
{
   int ierr=0;

   hypre_DistributedMatrixLocalStorage(matrix) = local_storage;

   return(ierr);
}

/*--------------------------------------------------------------------------
 * hypre_DistributedMatrixGetLocalStorage
 *--------------------------------------------------------------------------*/

void *
hypre_DistributedMatrixGetLocalStorage( hypre_DistributedMatrix *matrix  )
{
   return( hypre_DistributedMatrixLocalStorage(matrix) );

}


/*--------------------------------------------------------------------------
 * hypre_DistributedMatrixSetTranslator
 *--------------------------------------------------------------------------*/

int 
hypre_DistributedMatrixSetTranslator( hypre_DistributedMatrix *matrix,
				 void                  *translator  )
{
   hypre_DistributedMatrixTranslator(matrix) = translator;

   return(0);
}

/*--------------------------------------------------------------------------
 * hypre_DistributedMatrixGetTranslator
 *--------------------------------------------------------------------------*/

void *
hypre_DistributedMatrixGetTranslator( hypre_DistributedMatrix *matrix  )
{
   return( hypre_DistributedMatrixTranslator(matrix) );

}

/*--------------------------------------------------------------------------
 * hypre_DistributedMatrixSetAuxiliaryData
 *--------------------------------------------------------------------------*/

int 
hypre_DistributedMatrixSetAuxiliaryData( hypre_DistributedMatrix *matrix,
				 void                  *auxiliary_data  )
{
   hypre_DistributedMatrixAuxiliaryData(matrix) = auxiliary_data;

   return(0);
}

/*--------------------------------------------------------------------------
 * hypre_DistributedMatrixGetAuxiliaryData
 *--------------------------------------------------------------------------*/

void *
hypre_DistributedMatrixGetAuxiliaryData( hypre_DistributedMatrix *matrix  )
{
   return( hypre_DistributedMatrixAuxiliaryData(matrix) );

}

/*--------------------------------------------------------------------------
 * Optional routines that depend on underlying storage type
 *--------------------------------------------------------------------------*/

/*--------------------------------------------------------------------------
 * hypre_DistributedMatrixPrint
 *--------------------------------------------------------------------------*/

int 
hypre_DistributedMatrixPrint( hypre_DistributedMatrix *matrix )
{
    if ( hypre_DistributedMatrixLocalStorageType(matrix) == HYPRE_PETSC ) {
//      return( hypre_DistributedMatrixPrintPETSc( matrix ) );
      assert(0); // not implemented (deactivated by fk)
      return (-1);
    } else if ( hypre_DistributedMatrixLocalStorageType(matrix) == HYPRE_ISIS ) {
      assert(0); // not implemented (deactivated by fk)
//       return( hypre_PrintDistributedMatrixISIS( matrix ) );
      return (-1);
    } else if ( hypre_DistributedMatrixLocalStorageType(matrix) == HYPRE_PARCSR )
      return( hypre_DistributedMatrixPrintParCSR( matrix ) );
   else
      return(-1);
}

/*--------------------------------------------------------------------------
 * hypre_DistributedMatrixGetLocalRange
 *--------------------------------------------------------------------------*/

int 
hypre_DistributedMatrixGetLocalRange( hypre_DistributedMatrix *matrix,
                             int *row_start,
                             int *row_end,
                             int *col_start,
                             int *col_end )
{
    if ( hypre_DistributedMatrixLocalStorageType(matrix) == HYPRE_PETSC ) {
      assert(0); // not implemented (deactivated by fk)
//      return( hypre_DistributedMatrixGetLocalRangePETSc( matrix, row_start, row_end ) );
      return (-1);
    } else if ( hypre_DistributedMatrixLocalStorageType(matrix) == HYPRE_ISIS ) {
      assert(0); // not implemented (deactivated by fk)
//      return( hypre_GetDistributedMatrixLocalRangeISIS( matrix, row_start, row_end ) );
      return (-1);
    } else if ( hypre_DistributedMatrixLocalStorageType(matrix) == HYPRE_PARCSR )
      return( hypre_DistributedMatrixGetLocalRangeParCSR( matrix, row_start, row_end, col_start, col_end ) );
   else
      return(-1);
}

/*--------------------------------------------------------------------------
 * hypre_DistributedMatrixGetRow
 *--------------------------------------------------------------------------*/

int 
hypre_DistributedMatrixGetRow( hypre_DistributedMatrix *matrix,
                             int row,
                             int *size,
                             int **col_ind,
                             double **values )
{
   int ierr = 0;

#ifdef HYPRE_TIMING
   hypre_BeginTiming( matrix->GetRow_timer );
#endif

   if ( hypre_DistributedMatrixLocalStorageType(matrix) == HYPRE_PETSC ) {
//      ierr = hypre_DistributedMatrixGetRowPETSc( matrix, row, size, col_ind, values );
      assert(0); // not implemented
   }
   else if ( hypre_DistributedMatrixLocalStorageType(matrix) == HYPRE_ISIS ) {
//      ierr = hypre_GetDistributedMatrixRowISIS( matrix, row, size, col_ind, values );
      assert(0); // not implemented
   }
   else if ( hypre_DistributedMatrixLocalStorageType(matrix) == HYPRE_PARCSR ) {
      ierr = hypre_DistributedMatrixGetRowParCSR( matrix, row, size, col_ind, values );
   }
   else
      ierr = -1;

#ifdef HYPRE_TIMING
   hypre_EndTiming( matrix->GetRow_timer );
#endif

   return( ierr );
}

/*--------------------------------------------------------------------------
 * hypre_DistributedMatrixRestoreRow
 *--------------------------------------------------------------------------*/

int 
hypre_DistributedMatrixRestoreRow( hypre_DistributedMatrix *matrix,
                             int row,
                             int *size,
                             int **col_ind,
                             double **values )
{
   int ierr = 0;

#ifdef HYPRE_TIMING
   hypre_BeginTiming( matrix->GetRow_timer );
#endif

   if ( hypre_DistributedMatrixLocalStorageType(matrix) == HYPRE_PETSC )
//      ierr = hypre_DistributedMatrixRestoreRowPETSc( matrix, row, size, col_ind, values );
      assert(0); // not implemented
   else if ( hypre_DistributedMatrixLocalStorageType(matrix) == HYPRE_ISIS )
//      ierr = hypre_RestoreDistributedMatrixRowISIS( matrix, row, size, col_ind, values );
      assert(0); // not implemented
   else if ( hypre_DistributedMatrixLocalStorageType(matrix) == HYPRE_PARCSR )
      ierr = hypre_DistributedMatrixRestoreRowParCSR( matrix, row, size, col_ind, values );
   else
      ierr = -1;

#ifdef HYPRE_TIMING
   hypre_EndTiming( matrix->GetRow_timer );
#endif

   return( ierr );
}
