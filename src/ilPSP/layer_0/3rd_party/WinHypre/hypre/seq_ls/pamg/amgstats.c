/*BHEADER**********************************************************************
 * Copyright (c) 2008,  Lawrence Livermore National Security, LLC.
 * Produced at the Lawrence Livermore National Laboratory.
 * This file is part of HYPRE.  See file COPYRIGHT for details.
 *
 * HYPRE is free software; you can redistribute it and/or modify it under the
 * terms of the GNU Lesser General Public License (as published by the Free
 * Software Foundation) version 2.1 dated February 1999.
 *
 * $Revision: 2.6 $
 ***********************************************************************EHEADER*/





#include "headers.h"
#include "amg.h"

/*****************************************************************************
 *
 * Routine for getting matrix statistics from setup
 *
 *****************************************************************************/

int
hypre_AMGSetupStats( void *amg_vdata )
{
   hypre_AMGData *amg_data = amg_vdata;

   /* Data Structure variables */

   hypre_CSRMatrix **A_array;
   hypre_CSRMatrix **P_array;

   int      num_levels; 
   int      num_nonzeros;
/*   int      amg_ioutdat;
   char    *log_file_name;
*/ 

   /* Local variables */

   int      *A_i;
   double   *A_data;

   int      *P_i;
   double   *P_data;

   int       level;
   int       i,j;
   int       fine_size;
   int       coarse_size;
   int       entries;
   int       total_entries;
   int       min_entries;
   int       max_entries;
   double    avg_entries;
   double    rowsum;
   double    min_rowsum;
   double    max_rowsum;
   double    sparse;
   double    min_weight;
   double    max_weight;
   double    op_complxty=0;
   double    grid_complxty=0;
   double    num_nz0;
   double    num_var0;

   A_array = hypre_AMGDataAArray(amg_data);
   P_array = hypre_AMGDataPArray(amg_data);
   num_levels = hypre_AMGDataNumLevels(amg_data);
/*   amg_ioutdat = hypre_AMGDataIOutDat(amg_data);
   log_file_name = hypre_AMGDataLogFileName(amg_data);
*/    
   printf("\n  AMG SETUP PARAMETERS:\n\n");
   printf(" Strength threshold = %f\n",hypre_AMGDataStrongThreshold(amg_data));
   printf(" Max levels = %d\n",hypre_AMGDataMaxLevels(amg_data));
   printf(" Num levels = %d\n\n",num_levels);

   printf( "\nOperator Matrix Information:\n\n");

   printf("         nonzero         entries p");
   printf("er row        row sums\n");
   printf("lev rows entries  sparse  min max  ");
   printf("avg       min         max\n");
   printf("=======================================");
   printf("==========================\n");

  
   /*-----------------------------------------------------
    *  Enter Statistics Loop
    *-----------------------------------------------------*/

   num_var0 = (double) hypre_CSRMatrixNumRows(A_array[0]);
   num_nz0 = (double) hypre_CSRMatrixNumNonzeros(A_array[0]);
 
   for (level = 0; level < num_levels; level++)
   {
       A_i = hypre_CSRMatrixI(A_array[level]);
       A_data = hypre_CSRMatrixData(A_array[level]);

       fine_size = hypre_CSRMatrixNumRows(A_array[level]);
       num_nonzeros = hypre_CSRMatrixNumNonzeros(A_array[level]);
       sparse = num_nonzeros /((double) fine_size * (double) fine_size);
       op_complxty += ((double)num_nonzeros/num_nz0);
       grid_complxty += ((double)fine_size/num_var0);

       min_entries = A_i[1]-A_i[0];
       max_entries = 0;
       total_entries = 0;
       min_rowsum = 0.0;
       max_rowsum = 0.0;

       for (j = A_i[0]; j < A_i[1]; j++)
                    min_rowsum += A_data[j];

       max_rowsum = min_rowsum;

       for (j = 0; j < fine_size; j++)
       {
           entries = A_i[j+1] - A_i[j];
           min_entries = hypre_min(entries, min_entries);
           max_entries = hypre_max(entries, max_entries);
           total_entries += entries;

           rowsum = 0.0;
           for (i = A_i[j]; i < A_i[j+1]; i++)
               rowsum += A_data[i];

           min_rowsum = hypre_min(rowsum, min_rowsum);
           max_rowsum = hypre_max(rowsum, max_rowsum);
       }

       avg_entries = ((double) total_entries) / ((double) fine_size);

       printf( "%2d %5d %7d  %0.3f  %3d %3d",
                 level, fine_size, num_nonzeros, sparse, min_entries, 
                 max_entries);
       printf("  %4.1f  %10.3e  %10.3e\n", avg_entries,
                                 min_rowsum, max_rowsum);
   }
       
   printf( "\n\nInterpolation Matrix Information:\n\n");

   printf("                 entries/row    min     max");
   printf("         row sums\n");
   printf("lev  rows cols    min max  ");
   printf("   weight   weight     min       max \n");
   printf("=======================================");
   printf("==========================\n");

  
   /*-----------------------------------------------------
    *  Enter Statistics Loop
    *-----------------------------------------------------*/

   for (level = 0; level < num_levels-1; level++)
   {
       P_i = hypre_CSRMatrixI(P_array[level]);
       P_data = hypre_CSRMatrixData(P_array[level]);

       fine_size = hypre_CSRMatrixNumRows(P_array[level]);
       coarse_size = hypre_CSRMatrixNumCols(P_array[level]);
       num_nonzeros = hypre_CSRMatrixNumNonzeros(P_array[level]);

       min_entries = P_i[1]-P_i[0];
       max_entries = 0;
       total_entries = 0;
       min_rowsum = 0.0;
       max_rowsum = 0.0;
       min_weight = P_data[0];
       max_weight = 0.0;

       for (j = P_i[0]; j < P_i[1]; j++)
                    min_rowsum += P_data[j];

       max_rowsum = min_rowsum;

       for (j = 0; j < num_nonzeros; j++)
       {
          if (P_data[j] != 1.0)
          {
             min_weight = hypre_min(min_weight,P_data[j]);
             max_weight = hypre_max(max_weight,P_data[j]);
          }
       }

       for (j = 0; j < fine_size; j++)
       {
           entries = P_i[j+1] - P_i[j];
           min_entries = hypre_min(entries, min_entries);
           max_entries = hypre_max(entries, max_entries);
           total_entries += entries;

           rowsum = 0.0;
           for (i = P_i[j]; i < P_i[j+1]; i++)
               rowsum += P_data[i];

           min_rowsum = hypre_min(rowsum, min_rowsum);
           max_rowsum = hypre_max(rowsum, max_rowsum);
       }

       printf( "%2d %5d x %-5d %3d %3d",
             level, fine_size, coarse_size,  min_entries, max_entries);
       printf("  %5.3e  %5.3e %5.3e  %5.3e\n",
                 min_weight, max_weight, min_rowsum, max_rowsum);
   }
     
   printf("\n Operator Complexity: %8.3f\n", op_complxty); 
   printf(" Grid Complexity:     %8.3f\n", grid_complxty); 
   hypre_WriteSolverParams(amg_data);  
   
   return(0);
}  



/*---------------------------------------------------------------
 * hypre_WriteSolveParams
 *---------------------------------------------------------------*/


void     hypre_WriteSolverParams(data)
void    *data;
 
{
   hypre_AMGData  *amg_data = data;
 
   /* amg solve params */
   int      max_iter;
   int      cycle_type;    
   int     *num_grid_sweeps;  
   int     *grid_relax_type;   
   int    **grid_relax_points; 
   double   tol;
 
   /* amg output params */
   int      amg_ioutdat;
 
   int      j;
 
 
   /*----------------------------------------------------------
    * Get the amg_data data
    *----------------------------------------------------------*/
 
 
   max_iter   = hypre_AMGDataMaxIter(amg_data);
   cycle_type = hypre_AMGDataCycleType(amg_data);    
   num_grid_sweeps = hypre_AMGDataNumGridSweeps(amg_data);  
   grid_relax_type = hypre_AMGDataGridRelaxType(amg_data);
   grid_relax_points = hypre_AMGDataGridRelaxPoints(amg_data); 
   tol = hypre_AMGDataTol(amg_data);
 
   amg_ioutdat = hypre_AMGDataIOutDat(amg_data);
 
   /*----------------------------------------------------------
    * Open the output file
    *----------------------------------------------------------*/
 
   if (amg_ioutdat == 1 || amg_ioutdat == 3)
   { 
      printf("\n\nAMG SOLVER PARAMETERS:\n\n");
   
   /*----------------------------------------------------------
    * AMG info
    *----------------------------------------------------------*/
 
      printf( "  Maximum number of cycles:         %d \n",max_iter);
      printf( "  Stopping Tolerance:               %e \n",tol); 
      printf( "  Cycle type (1 = V, 2 = W, etc.):  %d\n\n", cycle_type);
      printf( "  Relaxation Parameters:\n");
      printf( "   Visiting Grid:            fine  down   up  coarse\n");
      printf( "   Number of partial sweeps:%4d  %4d   %2d  %4d \n",
              num_grid_sweeps[0],num_grid_sweeps[2],
              num_grid_sweeps[2],num_grid_sweeps[3]);
      printf( "   Type 0=Jac, 1=GS, 9=GE:  %4d  %4d   %2d  %4d \n",
              grid_relax_type[0],grid_relax_type[2],
              grid_relax_type[2],grid_relax_type[3]);
      printf( "   Point types, partial sweeps (1=C, -1=F):\n");
      printf( "                               Finest grid:");
      for (j = 0; j < num_grid_sweeps[0]; j++)
              printf("  %2d", grid_relax_points[0][j]);
      printf( "\n");
      printf( "                  Pre-CG relaxation (down):");
      for (j = 0; j < num_grid_sweeps[1]; j++)
              printf("  %2d", grid_relax_points[1][j]);
      printf( "\n");
      printf( "                   Post-CG relaxation (up):");
      for (j = 0; j < num_grid_sweeps[2]; j++)
              printf("  %2d", grid_relax_points[2][j]);
      printf( "\n");
      printf( "                             Coarsest grid:");
      for (j = 0; j < num_grid_sweeps[3]; j++)
              printf("  %2d", grid_relax_points[3][j]);
      printf( "\n\n");

      printf( " Output flag (ioutdat): %d \n", amg_ioutdat);
 
   /*----------------------------------------------------------
    * Close the output file
    *----------------------------------------------------------*/
 
   }
 
   return;
}
