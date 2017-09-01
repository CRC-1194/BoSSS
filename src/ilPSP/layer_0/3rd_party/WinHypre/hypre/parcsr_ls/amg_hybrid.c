/*BHEADER**********************************************************************
 * Copyright (c) 2008,  Lawrence Livermore National Security, LLC.
 * Produced at the Lawrence Livermore National Laboratory.
 * This file is part of HYPRE.  See file COPYRIGHT for details.
 *
 * HYPRE is free software; you can redistribute it and/or modify it under the
 * terms of the GNU Lesser General Public License (as published by the Free
 * Software Foundation) version 2.1 dated February 1999.
 *
 * $Revision: 2.28 $
 ***********************************************************************EHEADER*/




/******************************************************************************
 *
 *
 *****************************************************************************/

#include "headers.h"

/*--------------------------------------------------------------------------
 * hypre_AMGHybridData:
 *--------------------------------------------------------------------------*/

typedef struct
{

   double                tol;
   double                a_tol;
   double                cf_tol;
   int                   dscg_max_its;
   int                   pcg_max_its;
   int                   two_norm;
   int                   stop_crit;
   int                   rel_change;
   int                   solver_type;
   int                   k_dim;

   int                   pcg_default;              /* boolean */
   int                 (/*__stdcall*/ *pcg_precond_solve)();
   int                 (/*__stdcall*/ *pcg_precond_setup)();
   void                 *pcg_precond;
   void                 *pcg_solver;

   /* log info (always logged) */
   int                   dscg_num_its;
   int                   pcg_num_its;
   double                final_rel_res_norm;
   int                   time_index;

   /* additional information (place-holder currently used to print norms) */
   int                   logging;
   int                   print_level; 

   /* info for BoomerAMG */
   double		strong_threshold;
   double		max_row_sum;
   double		trunc_factor;
   int                  pmax;
   int                  setup_type;
   int			max_levels;
   int			measure_type;
   int			coarsen_type;
   int			interp_type;
   int			cycle_type;
   int		        relax_order;
   int		       *num_grid_sweeps;
   int		       *grid_relax_type;
   int		      **grid_relax_points;
   double	       *relax_weight;
   double	       *omega;
   int		        num_paths;
   int		        agg_num_levels;
   int		        num_functions;
   int		        nodal;
   int		       *dof_func;

} hypre_AMGHybridData;

/*--------------------------------------------------------------------------
 * hypre_AMGHybridCreate
 *--------------------------------------------------------------------------*/

void *
hypre_AMGHybridCreate( )
{
   hypre_AMGHybridData *AMGhybrid_data;

   AMGhybrid_data = hypre_CTAlloc(hypre_AMGHybridData, 1);

   (AMGhybrid_data -> time_index)  = hypre_InitializeTiming("AMGHybrid");

   /* set defaults */
   (AMGhybrid_data -> tol)               = 1.0e-06;
   (AMGhybrid_data -> a_tol)             = 0.0;
   (AMGhybrid_data -> cf_tol)            = 0.90;
   (AMGhybrid_data -> dscg_max_its)      = 1000;
   (AMGhybrid_data -> pcg_max_its)       = 200;
   (AMGhybrid_data -> two_norm)          = 0;
   (AMGhybrid_data -> stop_crit)         = 0;
   (AMGhybrid_data -> rel_change)        = 0;
   (AMGhybrid_data -> pcg_default)       = 1;
   (AMGhybrid_data -> solver_type)       = 1;
   (AMGhybrid_data -> pcg_precond_solve) = NULL;
   (AMGhybrid_data -> pcg_precond_setup) = NULL;
   (AMGhybrid_data -> pcg_precond)       = NULL;
   (AMGhybrid_data -> pcg_solver)       = NULL;
   
   /* initialize */ 
   (AMGhybrid_data -> dscg_num_its)      = 0; 
   (AMGhybrid_data -> pcg_num_its)       = 0; 
   (AMGhybrid_data -> logging)           = 0; 
   (AMGhybrid_data -> print_level)       = 0; 
   (AMGhybrid_data -> k_dim)             = 5; 

   /* BoomerAMG info */
   (AMGhybrid_data -> setup_type)       = 1;
   (AMGhybrid_data -> strong_threshold)  = 0.25;
   (AMGhybrid_data -> max_row_sum)  = 0.9;
   (AMGhybrid_data -> trunc_factor)  = 0.0;
   (AMGhybrid_data -> pmax)  = 0;
   (AMGhybrid_data -> max_levels)  = 25;
   (AMGhybrid_data -> measure_type)  = 0;
   (AMGhybrid_data -> coarsen_type)  = 6;
   (AMGhybrid_data -> interp_type)  = 0;
   (AMGhybrid_data -> cycle_type)  = 1;
   (AMGhybrid_data -> relax_order)  = 1;
   (AMGhybrid_data -> num_grid_sweeps)  = NULL;
   (AMGhybrid_data -> grid_relax_type)  = NULL;
   (AMGhybrid_data -> grid_relax_points)  = NULL;
   (AMGhybrid_data -> relax_weight)  = NULL;
   (AMGhybrid_data -> omega)  = NULL;
   (AMGhybrid_data -> agg_num_levels)  = 0;
   (AMGhybrid_data -> num_paths)  = 1;
   (AMGhybrid_data -> num_functions)  = 1;
   (AMGhybrid_data -> nodal)  = 0;
   (AMGhybrid_data -> dof_func)  = NULL;

   return (void *) AMGhybrid_data; 
}

/*-------------------------------------------------------------------------- *
  hypre_AMGHybridDestroy 
*--------------------------------------------------------------------------*/ 

int
hypre_AMGHybridDestroy( void  *AMGhybrid_vdata )
{
   hypre_AMGHybridData *AMGhybrid_data = AMGhybrid_vdata;
   int i;
   int solver_type = (AMGhybrid_data -> solver_type);
   /*int pcg_default = (AMGhybrid_data -> pcg_default);*/
   void *pcg_solver = (AMGhybrid_data -> pcg_solver);
   void *pcg_precond = (AMGhybrid_data -> pcg_precond);

   if (pcg_precond) hypre_BoomerAMGDestroy(pcg_precond);
   if (solver_type == 1) hypre_PCGDestroy(pcg_solver);
   if (solver_type == 2) hypre_GMRESDestroy(pcg_solver);
   if (solver_type == 3) hypre_BiCGSTABDestroy(pcg_solver);

   if (AMGhybrid_data -> num_grid_sweeps)  
   {
      hypre_TFree( (AMGhybrid_data -> num_grid_sweeps) );
      (AMGhybrid_data -> num_grid_sweeps) = NULL;
   }
   if (AMGhybrid_data -> grid_relax_type)  
   {
      hypre_TFree( (AMGhybrid_data -> grid_relax_type) );
      (AMGhybrid_data -> grid_relax_type) = NULL;
   }
   if (AMGhybrid_data -> grid_relax_points)  
   {
      for (i=0; i < 4; i++)
         hypre_TFree( (AMGhybrid_data -> grid_relax_points)[i] );
      hypre_TFree( (AMGhybrid_data -> grid_relax_points) );
      (AMGhybrid_data -> grid_relax_points) = NULL;
   }
   if (AMGhybrid_data -> relax_weight)  
   {
      hypre_TFree( (AMGhybrid_data -> relax_weight) );
      (AMGhybrid_data -> relax_weight) = NULL;
   }
   if (AMGhybrid_data -> omega)  
   {
      hypre_TFree( (AMGhybrid_data -> omega) );
      (AMGhybrid_data -> omega) = NULL;
   }
   if (AMGhybrid_data -> dof_func)  
   {
      hypre_TFree( (AMGhybrid_data -> dof_func) );
      (AMGhybrid_data -> dof_func) = NULL;
   }
   if (AMGhybrid_data)
   {
      hypre_TFree(AMGhybrid_data);
   }

   return hypre_error_flag;
}

/*--------------------------------------------------------------------------
 * hypre_AMGHybridSetTol
 *--------------------------------------------------------------------------*/

int
hypre_AMGHybridSetTol( void   *AMGhybrid_vdata,
                    double  tol       )
{
   hypre_AMGHybridData *AMGhybrid_data = AMGhybrid_vdata;

   if (!AMGhybrid_data)
   {
      hypre_error_in_arg(1);
      return hypre_error_flag;
   }
   if (tol < 0 || tol > 1)
   {
      hypre_error_in_arg(2);
      return hypre_error_flag;
   }
   (AMGhybrid_data -> tol) = tol;

   return hypre_error_flag;
}
/*--------------------------------------------------------------------------
 * hypre_AMGHybridSetAbsoluteTol
 *--------------------------------------------------------------------------*/

int
hypre_AMGHybridSetAbsoluteTol( void   *AMGhybrid_vdata,
                    double  a_tol       )
{
   hypre_AMGHybridData *AMGhybrid_data = AMGhybrid_vdata;

   if (!AMGhybrid_data)
   {
      hypre_error_in_arg(1);
      return hypre_error_flag;
   }
   if (a_tol < 0 || a_tol > 1)
   {
      hypre_error_in_arg(2);
      return hypre_error_flag;
   }
   (AMGhybrid_data -> a_tol) = a_tol;

   return hypre_error_flag;
}
/*--------------------------------------------------------------------------
 * hypre_AMGHybridSetConvergenceTol
 *--------------------------------------------------------------------------*/

int
hypre_AMGHybridSetConvergenceTol( void   *AMGhybrid_vdata,
                               double  cf_tol       )
{
   hypre_AMGHybridData *AMGhybrid_data = AMGhybrid_vdata;
   if (!AMGhybrid_data)
   {
      hypre_error_in_arg(1);
      return hypre_error_flag;
   }
   if (cf_tol < 0 || cf_tol > 1)
   {
      hypre_error_in_arg(2);
      return hypre_error_flag;
   }

   (AMGhybrid_data -> cf_tol) = cf_tol;

   return hypre_error_flag;
}

/*--------------------------------------------------------------------------
 * hypre_AMGHybridSetDSCGMaxIter
 *--------------------------------------------------------------------------*/

int
hypre_AMGHybridSetDSCGMaxIter( void   *AMGhybrid_vdata,
                            int     dscg_max_its )
{
   hypre_AMGHybridData *AMGhybrid_data = AMGhybrid_vdata;
   if (!AMGhybrid_data)
   {
      hypre_error_in_arg(1);
      return hypre_error_flag;
   }
   if (dscg_max_its < 0)
   {
      hypre_error_in_arg(2);
      return hypre_error_flag;
   }

   (AMGhybrid_data -> dscg_max_its) = dscg_max_its;

   return hypre_error_flag;
}

/*--------------------------------------------------------------------------
 * hypre_AMGHybridSetPCGMaxIter
 *--------------------------------------------------------------------------*/

int
hypre_AMGHybridSetPCGMaxIter( void   *AMGhybrid_vdata,
                           int     pcg_max_its  )
{
   hypre_AMGHybridData *AMGhybrid_data = AMGhybrid_vdata;
   if (!AMGhybrid_data)
   {
      hypre_error_in_arg(1);
      return hypre_error_flag;
   }
   if (pcg_max_its < 0)
   {
      hypre_error_in_arg(2);
      return hypre_error_flag;
   }

   (AMGhybrid_data -> pcg_max_its) = pcg_max_its;

   return hypre_error_flag;
}

/*--------------------------------------------------------------------------
 * hypre_AMGHybridSetSetupType
 *--------------------------------------------------------------------------*/

int
hypre_AMGHybridSetSetupType( void   *AMGhybrid_vdata,
                           int     setup_type  )
{
   hypre_AMGHybridData *AMGhybrid_data = AMGhybrid_vdata;
   if (!AMGhybrid_data)
   {
      hypre_error_in_arg(1);
      return hypre_error_flag;
   }

   (AMGhybrid_data -> setup_type) = setup_type;

   return hypre_error_flag;
}

/*--------------------------------------------------------------------------
 * hypre_AMGHybridSetSolverType
 *--------------------------------------------------------------------------*/

int
hypre_AMGHybridSetSolverType( void   *AMGhybrid_vdata,
                           int     solver_type  )
{
   hypre_AMGHybridData *AMGhybrid_data = AMGhybrid_vdata;
   if (!AMGhybrid_data)
   {
      hypre_error_in_arg(1);
      return hypre_error_flag;
   }

   (AMGhybrid_data -> solver_type) = solver_type;

   return hypre_error_flag;
}

/*--------------------------------------------------------------------------
 * hypre_AMGHybridSetKDim
 *--------------------------------------------------------------------------*/

int
hypre_AMGHybridSetKDim( void   *AMGhybrid_vdata,
                           int     k_dim  )
{
   hypre_AMGHybridData *AMGhybrid_data = AMGhybrid_vdata;
   if (!AMGhybrid_data)
   {
      hypre_error_in_arg(1);
      return hypre_error_flag;
   }
   if (k_dim < 1)
   {
      hypre_error_in_arg(2);
      return hypre_error_flag;
   }

   (AMGhybrid_data -> k_dim) = k_dim;

   return hypre_error_flag;
}

/*--------------------------------------------------------------------------
 * hypre_AMGHybridSetStopCrit
 *--------------------------------------------------------------------------*/

int
hypre_AMGHybridSetStopCrit( void *AMGhybrid_vdata,
                        int   stop_crit  )
{
   hypre_AMGHybridData *AMGhybrid_data = AMGhybrid_vdata;
   if (!AMGhybrid_data)
   {
      hypre_error_in_arg(1);
      return hypre_error_flag;
   }

   (AMGhybrid_data -> stop_crit) = stop_crit;

   return hypre_error_flag;
}

/*--------------------------------------------------------------------------
 * hypre_AMGHybridSetTwoNorm
 *--------------------------------------------------------------------------*/

int
hypre_AMGHybridSetTwoNorm( void *AMGhybrid_vdata,
                        int   two_norm  )
{
   hypre_AMGHybridData *AMGhybrid_data = AMGhybrid_vdata;
   if (!AMGhybrid_data)
   {
      hypre_error_in_arg(1);
      return hypre_error_flag;
   }

   (AMGhybrid_data -> two_norm) = two_norm;

   return hypre_error_flag;
}

/*--------------------------------------------------------------------------
 * hypre_AMGHybridSetRelChange
 *--------------------------------------------------------------------------*/

int
hypre_AMGHybridSetRelChange( void *AMGhybrid_vdata,
                          int   rel_change  )
{
   hypre_AMGHybridData *AMGhybrid_data = AMGhybrid_vdata;
   if (!AMGhybrid_data)
   {
      hypre_error_in_arg(1);
      return hypre_error_flag;
   }

   (AMGhybrid_data -> rel_change) = rel_change;

   return hypre_error_flag;
}

/*--------------------------------------------------------------------------
 * hypre_AMGHybridSetPrecond
 *--------------------------------------------------------------------------*/

int
hypre_AMGHybridSetPrecond( void  *pcg_vdata,
                        int  (/*__stdcall*/ *pcg_precond_solve)(),
                        int  (/*__stdcall*/ *pcg_precond_setup)(),
                        void  *pcg_precond          )
{
   hypre_AMGHybridData *pcg_data = pcg_vdata;
   if (!pcg_data)
   {
      hypre_error_in_arg(1);
      return hypre_error_flag;
   }
 
   (pcg_data -> pcg_default)       = 0;
   (pcg_data -> pcg_precond_solve) = pcg_precond_solve;
   (pcg_data -> pcg_precond_setup) = pcg_precond_setup;
   (pcg_data -> pcg_precond)       = pcg_precond;
 
   return hypre_error_flag;
}

/*--------------------------------------------------------------------------
 * hypre_AMGHybridSetLogging
 *--------------------------------------------------------------------------*/

int
hypre_AMGHybridSetLogging( void *AMGhybrid_vdata,
                        int   logging  )
{
   hypre_AMGHybridData *AMGhybrid_data = AMGhybrid_vdata;
   if (!AMGhybrid_data)
   {
      hypre_error_in_arg(1);
      return hypre_error_flag;
   }

   (AMGhybrid_data -> logging) = logging;

   return hypre_error_flag;
}

/*--------------------------------------------------------------------------
 * hypre_AMGHybridSetPrintLevel
 *--------------------------------------------------------------------------*/

int
hypre_AMGHybridSetPrintLevel( void *AMGhybrid_vdata,
                        int   print_level  )
{
   hypre_AMGHybridData *AMGhybrid_data = AMGhybrid_vdata;
   if (!AMGhybrid_data)
   {
      hypre_error_in_arg(1);
      return hypre_error_flag;
   }

   (AMGhybrid_data -> print_level) = print_level;

   return hypre_error_flag;
}

/*--------------------------------------------------------------------------
 * hypre_AMGHybridSetStrongThreshold
 *--------------------------------------------------------------------------*/

int
hypre_AMGHybridSetStrongThreshold( void *AMGhybrid_vdata,
                        double strong_threshold)
{
   hypre_AMGHybridData *AMGhybrid_data = AMGhybrid_vdata;
   if (!AMGhybrid_data)
   {
      hypre_error_in_arg(1);
      return hypre_error_flag;
   }
   if (strong_threshold < 0 || strong_threshold > 1)
   {
      hypre_error_in_arg(2);
      return hypre_error_flag;
   }

   (AMGhybrid_data -> strong_threshold) = strong_threshold;

   return hypre_error_flag;
}

/*--------------------------------------------------------------------------
 * hypre_AMGHybridSetMaxRowSum
 *--------------------------------------------------------------------------*/

int
hypre_AMGHybridSetMaxRowSum( void *AMGhybrid_vdata,
                        double   max_row_sum  )
{
   hypre_AMGHybridData *AMGhybrid_data = AMGhybrid_vdata;
   if (!AMGhybrid_data)
   {
      hypre_error_in_arg(1);
      return hypre_error_flag;
   }
   if (max_row_sum < 0 || max_row_sum > 1)
   {
      hypre_error_in_arg(2);
      return hypre_error_flag;
   }

   (AMGhybrid_data -> max_row_sum) = max_row_sum;

   return hypre_error_flag;
}

/*--------------------------------------------------------------------------
 * hypre_AMGHybridSetTruncFactor
 *--------------------------------------------------------------------------*/

int
hypre_AMGHybridSetTruncFactor( void *AMGhybrid_vdata,
                        double   trunc_factor  )
{
   hypre_AMGHybridData *AMGhybrid_data = AMGhybrid_vdata;
   if (!AMGhybrid_data)
   {
      hypre_error_in_arg(1);
      return hypre_error_flag;
   }
   if (trunc_factor < 0 || trunc_factor > 1)
   {
      hypre_error_in_arg(2);
      return hypre_error_flag;
   }

   (AMGhybrid_data -> trunc_factor) = trunc_factor;

   return hypre_error_flag;
}

/*--------------------------------------------------------------------------
 * hypre_AMGHybridSetPMaxElmts
 *--------------------------------------------------------------------------*/

int
hypre_AMGHybridSetPMaxElmts( void   *AMGhybrid_vdata,
                             int    P_max_elmts )
{

   hypre_AMGHybridData *AMGhybrid_data = AMGhybrid_vdata;
   if (!AMGhybrid_data)
   {
      hypre_error_in_arg(1);
      return hypre_error_flag;
   }

   if (P_max_elmts < 0)
   {
      hypre_error_in_arg(2);
      return hypre_error_flag;
   }

    (AMGhybrid_data -> pmax) = P_max_elmts;

   return hypre_error_flag;
}




/*--------------------------------------------------------------------------
 * hypre_AMGHybridSetMaxLevels
 *--------------------------------------------------------------------------*/

int
hypre_AMGHybridSetMaxLevels( void *AMGhybrid_vdata,
                        int   max_levels  )
{
   hypre_AMGHybridData *AMGhybrid_data = AMGhybrid_vdata;
   if (!AMGhybrid_data)
   {
      hypre_error_in_arg(1);
      return hypre_error_flag;
   }
   if (max_levels < 1)
   {
      hypre_error_in_arg(2);
      return hypre_error_flag;
   }

   (AMGhybrid_data -> max_levels) = max_levels;

   return hypre_error_flag;
}

/*--------------------------------------------------------------------------
 * hypre_AMGHybridSetMeasureType
 *--------------------------------------------------------------------------*/

int
hypre_AMGHybridSetMeasureType( void *AMGhybrid_vdata,
                        int   measure_type  )
{
   hypre_AMGHybridData *AMGhybrid_data = AMGhybrid_vdata;
   if (!AMGhybrid_data)
   {
      hypre_error_in_arg(1);
      return hypre_error_flag;
   }

   (AMGhybrid_data -> measure_type) = measure_type;

   return hypre_error_flag;
}

/*--------------------------------------------------------------------------
 * hypre_AMGHybridSetCoarsenType
 *--------------------------------------------------------------------------*/

int
hypre_AMGHybridSetCoarsenType( void *AMGhybrid_vdata,
                        int   coarsen_type  )
{
   hypre_AMGHybridData *AMGhybrid_data = AMGhybrid_vdata;
   if (!AMGhybrid_data)
   {
      hypre_error_in_arg(1);
      return hypre_error_flag;
   }

   (AMGhybrid_data -> coarsen_type) = coarsen_type;

   return hypre_error_flag;
}

/*--------------------------------------------------------------------------
 * hypre_AMGHybridSetInterpType
 *--------------------------------------------------------------------------*/

int
hypre_AMGHybridSetInterpType( void *AMGhybrid_vdata,
                        int   interp_type  )
{
   hypre_AMGHybridData *AMGhybrid_data = AMGhybrid_vdata;
   if (!AMGhybrid_data)
   {
      hypre_error_in_arg(1);
      return hypre_error_flag;
   }
   if (interp_type < 0)
   {
      hypre_error_in_arg(2);
      return hypre_error_flag;
   }

   (AMGhybrid_data -> interp_type) = interp_type;

   return hypre_error_flag;
}

/*--------------------------------------------------------------------------
 * hypre_AMGHybridSetCycleType
 *--------------------------------------------------------------------------*/

int
hypre_AMGHybridSetCycleType( void *AMGhybrid_vdata,
                        int   cycle_type  )
{
   hypre_AMGHybridData *AMGhybrid_data = AMGhybrid_vdata;
   if (!AMGhybrid_data)
   {
      hypre_error_in_arg(1);
      return hypre_error_flag;
   }
   if (cycle_type < 1 || cycle_type > 2)
   {
      hypre_error_in_arg(2);
      return hypre_error_flag;
   }

   (AMGhybrid_data -> cycle_type) = cycle_type;

   return hypre_error_flag;
}

/*--------------------------------------------------------------------------
 * hypre_AMGHybridSetNumSweeps
 *--------------------------------------------------------------------------*/

int
hypre_AMGHybridSetNumSweeps( void *AMGhybrid_vdata,
                        int   num_sweeps  )
{
   hypre_AMGHybridData *AMGhybrid_data = AMGhybrid_vdata;
   int                 *num_grid_sweeps;
   int               i;
   if (!AMGhybrid_data)
   {
      hypre_error_in_arg(1);
      return hypre_error_flag;
   }
   if (num_sweeps < 1)
   {
      hypre_error_in_arg(2);
      return hypre_error_flag;
   }

   if ((AMGhybrid_data -> num_grid_sweeps) == NULL)
      (AMGhybrid_data -> num_grid_sweeps) = hypre_CTAlloc(int,4);
   num_grid_sweeps = (AMGhybrid_data -> num_grid_sweeps);
   for (i=0; i < 3; i++)
   {
      num_grid_sweeps[i] = num_sweeps;
   }
   num_grid_sweeps[3] = 1;
   return hypre_error_flag;
}

/*--------------------------------------------------------------------------
 * hypre_AMGHybridSetCycleNumSweeps
 *--------------------------------------------------------------------------*/

int
hypre_AMGHybridSetCycleNumSweeps( void *AMGhybrid_vdata,
                                  int   num_sweeps,
                                  int   k)
{
   hypre_AMGHybridData *AMGhybrid_data = AMGhybrid_vdata;
   int                 *num_grid_sweeps;
   int               i;

   if (!AMGhybrid_data)
   {
      hypre_error_in_arg(1);
      return hypre_error_flag;
   }
   if (num_sweeps < 1)
   {
      hypre_error_in_arg(2);
      return hypre_error_flag;
   }
   if (k < 1 || k > 3)
   {
      printf (" Warning! Invalid cycle! num_sweeps not set!\n");
      hypre_error_in_arg(3);
      return hypre_error_flag;
   }

   num_grid_sweeps = (AMGhybrid_data -> num_grid_sweeps);
   if (num_grid_sweeps == NULL)
   {
      (AMGhybrid_data -> num_grid_sweeps) = hypre_CTAlloc(int,4);
      num_grid_sweeps = (AMGhybrid_data -> num_grid_sweeps);
      for (i=0; i < 4; i++)
      {
          num_grid_sweeps[i] = 1;
      }
   }
   num_grid_sweeps[k] = num_sweeps;
   return hypre_error_flag;
}

/*--------------------------------------------------------------------------
 * hypre_AMGHybridSetRelaxType
 *--------------------------------------------------------------------------*/

int
hypre_AMGHybridSetRelaxType( void *AMGhybrid_vdata,
                        int  relax_type  )
{
   hypre_AMGHybridData *AMGhybrid_data = AMGhybrid_vdata;
   int               *grid_relax_type;
   int               i;
   if (!AMGhybrid_data)
   {
      hypre_error_in_arg(1);
      return hypre_error_flag;
   }

   if ((AMGhybrid_data -> grid_relax_type) == NULL )
      (AMGhybrid_data -> grid_relax_type) = hypre_CTAlloc(int,4);
   grid_relax_type = (AMGhybrid_data -> grid_relax_type);
   for (i=0; i < 3; i++)
      grid_relax_type[i] = relax_type;
   grid_relax_type[3] = 9;

   return hypre_error_flag;
}

/*--------------------------------------------------------------------------
 * hypre_AMGHybridSetCycleRelaxType
 *--------------------------------------------------------------------------*/

int
hypre_AMGHybridSetCycleRelaxType( void *AMGhybrid_vdata,
                                  int   relax_type,
                                  int   k  )
{
   hypre_AMGHybridData *AMGhybrid_data = AMGhybrid_vdata;
   int                 *grid_relax_type;
   int                 i;
   if (!AMGhybrid_data)
   {
      hypre_error_in_arg(1);
      return hypre_error_flag;
   }

   if (k<1 || k > 3)
   {
      printf (" Warning! Invalid cycle! Relax type not set!\n");
      hypre_error_in_arg(3);
      return hypre_error_flag;
   }

   grid_relax_type = (AMGhybrid_data -> grid_relax_type);
   if (grid_relax_type == NULL )
   {
      (AMGhybrid_data -> grid_relax_type) = hypre_CTAlloc(int,4);
      grid_relax_type = (AMGhybrid_data -> grid_relax_type);
      for (i=0; i < 3; i++)
         grid_relax_type[i] = 3;
      grid_relax_type[3] = 9;
   }
   grid_relax_type[k] = relax_type;

   return hypre_error_flag;
}

/*--------------------------------------------------------------------------
 * hypre_AMGHybridSetRelaxOrder
 *--------------------------------------------------------------------------*/

int
hypre_AMGHybridSetRelaxOrder( void *AMGhybrid_vdata,
                              int   relax_order  )
{
   hypre_AMGHybridData *AMGhybrid_data = AMGhybrid_vdata;
   if (!AMGhybrid_data)
   {
      hypre_error_in_arg(1);
      return hypre_error_flag;
   }

   (AMGhybrid_data -> relax_order) = relax_order;

   return hypre_error_flag;
}

/*--------------------------------------------------------------------------
 * hypre_AMGHybridSetNumGridSweeps
 *--------------------------------------------------------------------------*/

int
hypre_AMGHybridSetNumGridSweeps( void *AMGhybrid_vdata,
                        int  *num_grid_sweeps  )
{
   hypre_AMGHybridData *AMGhybrid_data = AMGhybrid_vdata;
   if (!AMGhybrid_data)
   {
      hypre_error_in_arg(1);
      return hypre_error_flag;
   }
   if (!num_grid_sweeps)
   {
      hypre_error_in_arg(2);
      return hypre_error_flag;
   }

   if ((AMGhybrid_data -> num_grid_sweeps) != NULL)
      hypre_TFree((AMGhybrid_data -> num_grid_sweeps));
   (AMGhybrid_data -> num_grid_sweeps) = num_grid_sweeps;

   return hypre_error_flag;
}

/*--------------------------------------------------------------------------
 * hypre_AMGHybridSetGridRelaxType
 *--------------------------------------------------------------------------*/

int
hypre_AMGHybridSetGridRelaxType( void *AMGhybrid_vdata,
                        int  *grid_relax_type  )
{
   hypre_AMGHybridData *AMGhybrid_data = AMGhybrid_vdata;
   if (!AMGhybrid_data)
   {
      hypre_error_in_arg(1);
      return hypre_error_flag;
   }
   if (!grid_relax_type)
   {
      hypre_error_in_arg(2);
      return hypre_error_flag;
   }

   if ((AMGhybrid_data -> grid_relax_type) != NULL )
      hypre_TFree((AMGhybrid_data -> grid_relax_type));
   (AMGhybrid_data -> grid_relax_type) = grid_relax_type;

   return hypre_error_flag;
}

/*--------------------------------------------------------------------------
 * hypre_AMGHybridSetGridRelaxPoints
 *--------------------------------------------------------------------------*/

int
hypre_AMGHybridSetGridRelaxPoints( void *AMGhybrid_vdata,
                        int  **grid_relax_points  )
{
   hypre_AMGHybridData *AMGhybrid_data = AMGhybrid_vdata;
   if (!AMGhybrid_data)
   {
      hypre_error_in_arg(1);
      return hypre_error_flag;
   }
   if (!grid_relax_points)
   {
      hypre_error_in_arg(2);
      return hypre_error_flag;
   }

   if ((AMGhybrid_data -> grid_relax_points) != NULL )
      hypre_TFree((AMGhybrid_data -> grid_relax_points));
   (AMGhybrid_data -> grid_relax_points) = grid_relax_points;

   return hypre_error_flag;
}

/*--------------------------------------------------------------------------
 * hypre_AMGHybridSetRelaxWeight
 *--------------------------------------------------------------------------*/

int
hypre_AMGHybridSetRelaxWeight( void *AMGhybrid_vdata,
                        double *relax_weight  )
{
   hypre_AMGHybridData *AMGhybrid_data = AMGhybrid_vdata;
   if (!AMGhybrid_data)
   {
      hypre_error_in_arg(1);
      return hypre_error_flag;
   }
   if (!relax_weight)
   {
      hypre_error_in_arg(2);
      return hypre_error_flag;
   }

   if ((AMGhybrid_data -> relax_weight) != NULL )
      hypre_TFree((AMGhybrid_data -> relax_weight));
   (AMGhybrid_data -> relax_weight) = relax_weight;

   return hypre_error_flag;
}

/*--------------------------------------------------------------------------
 * hypre_AMGHybridSetOmega
 *--------------------------------------------------------------------------*/

int
hypre_AMGHybridSetOmega( void *AMGhybrid_vdata,
                        double *omega  )
{
   hypre_AMGHybridData *AMGhybrid_data = AMGhybrid_vdata;
   if (!AMGhybrid_data)
   {
      hypre_error_in_arg(1);
      return hypre_error_flag;
   }
   if (!omega)
   {
      hypre_error_in_arg(2);
      return hypre_error_flag;
   }

   if ((AMGhybrid_data -> omega) != NULL )
      hypre_TFree((AMGhybrid_data -> omega));
   (AMGhybrid_data -> omega) = omega;

   return hypre_error_flag;
}

/*--------------------------------------------------------------------------
 * hypre_AMGHybridSetRelaxWt
 *--------------------------------------------------------------------------*/

int
hypre_AMGHybridSetRelaxWt( void *AMGhybrid_vdata,
                        double  relax_wt  )
{
   hypre_AMGHybridData *AMGhybrid_data = AMGhybrid_vdata;
   int               i , num_levels;
   double	       *relax_wt_array;
   if (!AMGhybrid_data)
   {
      hypre_error_in_arg(1);
      return hypre_error_flag;
   }

   num_levels = (AMGhybrid_data -> max_levels);
   relax_wt_array = (AMGhybrid_data -> relax_weight);
   if (relax_wt_array == NULL)
   {
      relax_wt_array = hypre_CTAlloc(double,num_levels);
      (AMGhybrid_data -> relax_weight) = relax_wt_array;
   }
   for (i=0; i < num_levels; i++)
      relax_wt_array[i] = relax_wt;

   return hypre_error_flag;
}

/*--------------------------------------------------------------------------
 * hypre_AMGHybridSetLevelRelaxWt
 *--------------------------------------------------------------------------*/

int
hypre_AMGHybridSetLevelRelaxWt( void   *AMGhybrid_vdata,
                                double  relax_wt,
                                int     level  )
{
   hypre_AMGHybridData *AMGhybrid_data = AMGhybrid_vdata;
   int                i , num_levels;
   double	       *relax_wt_array;
   if (!AMGhybrid_data)
   {
      hypre_error_in_arg(1);
      return hypre_error_flag;
   }

   num_levels = (AMGhybrid_data -> max_levels);
   if (level > num_levels-1) 
   {
      printf (" Warning! Invalid level! Relax weight not set!\n");
      hypre_error_in_arg(3);
      return hypre_error_flag;
   }
   relax_wt_array = (AMGhybrid_data -> relax_weight);
   if (relax_wt_array == NULL)
   {
      relax_wt_array = hypre_CTAlloc(double,num_levels);
      for (i=0; i < num_levels; i++)
         relax_wt_array[i] = 1.0;
      (AMGhybrid_data -> relax_weight) = relax_wt_array;
   }
   relax_wt_array[level] = relax_wt;

   return hypre_error_flag;
}

/*--------------------------------------------------------------------------
 * hypre_AMGHybridSetOuterWt
 *--------------------------------------------------------------------------*/

int
hypre_AMGHybridSetOuterWt( void *AMGhybrid_vdata,
                        double  outer_wt  )
{
   hypre_AMGHybridData *AMGhybrid_data = AMGhybrid_vdata;
   int                i , num_levels;
   double	       *outer_wt_array;
   if (!AMGhybrid_data)
   {
      hypre_error_in_arg(1);
      return hypre_error_flag;
   }

   num_levels = (AMGhybrid_data -> max_levels);
   outer_wt_array = (AMGhybrid_data -> omega);
   if (outer_wt_array == NULL)
   {
      outer_wt_array = hypre_CTAlloc(double,num_levels);
      (AMGhybrid_data -> omega) = outer_wt_array;
   }
   for (i=0; i < num_levels; i++)
      outer_wt_array[i] = outer_wt;

   return hypre_error_flag;
}

/*--------------------------------------------------------------------------
 * hypre_AMGHybridSetLevelOuterWt
 *--------------------------------------------------------------------------*/

int
hypre_AMGHybridSetLevelOuterWt( void   *AMGhybrid_vdata,
                                double  outer_wt,
                                int     level  )
{
   hypre_AMGHybridData *AMGhybrid_data = AMGhybrid_vdata;
   int                i , num_levels;
   double	       *outer_wt_array;
   if (!AMGhybrid_data)
   {
      hypre_error_in_arg(1);
      return hypre_error_flag;
   }

   num_levels = (AMGhybrid_data -> max_levels);
   if (level > num_levels-1) 
   {
      printf (" Warning! Invalid level! Outer weight not set!\n");
      hypre_error_in_arg(3);
      return hypre_error_flag;
   }
   outer_wt_array = (AMGhybrid_data -> omega);
   if (outer_wt_array == NULL)
   {
      outer_wt_array = hypre_CTAlloc(double,num_levels);
      for (i=0; i < num_levels; i++)
         outer_wt_array[i] = 1.0;
      (AMGhybrid_data -> omega) = outer_wt_array;
   }
   outer_wt_array[level] = outer_wt;

   return hypre_error_flag;
}

/*--------------------------------------------------------------------------
 * hypre_AMGHybridSetNumPaths
 *--------------------------------------------------------------------------*/
                                                                                                                  
int
hypre_AMGHybridSetNumPaths( void   *AMGhybrid_vdata,
                              int    num_paths      )
{
   hypre_AMGHybridData *AMGhybrid_data = AMGhybrid_vdata;
   if (!AMGhybrid_data)
   {
      hypre_error_in_arg(1);
      return hypre_error_flag;
   }
   if (num_paths < 1)
   {
      hypre_error_in_arg(2);
      return hypre_error_flag;
   }

   (AMGhybrid_data -> num_paths) = num_paths;

   return hypre_error_flag;
}
                                                                                                                  
/*--------------------------------------------------------------------------
 * hypre_AMGHybridSetDofFunc
 *--------------------------------------------------------------------------*/
                                                                                                                  
int
hypre_AMGHybridSetDofFunc( void *AMGhybrid_vdata,
                        int *dof_func  )
{
   hypre_AMGHybridData *AMGhybrid_data = AMGhybrid_vdata;
   if (!AMGhybrid_data)
   {
      hypre_error_in_arg(1);
      return hypre_error_flag;
   }
   if (!dof_func)
   {
      hypre_error_in_arg(2);
      return hypre_error_flag;
   }

   if ((AMGhybrid_data -> dof_func) != NULL )
      hypre_TFree((AMGhybrid_data -> dof_func));
   (AMGhybrid_data -> dof_func) = dof_func;

   return hypre_error_flag;
}
/*--------------------------------------------------------------------------
 * hypre_AMGHybridSetAggNumLevels
 *--------------------------------------------------------------------------*/
                                                                                                                  
int
hypre_AMGHybridSetAggNumLevels( void   *AMGhybrid_vdata,
                              int    agg_num_levels      )
{
   hypre_AMGHybridData *AMGhybrid_data = AMGhybrid_vdata;
   if (!AMGhybrid_data)
   {
      hypre_error_in_arg(1);
      return hypre_error_flag;
   }
   if (agg_num_levels < 0)
   {
      hypre_error_in_arg(2);
      return hypre_error_flag;
   }
                                                                                                                  
   (AMGhybrid_data -> agg_num_levels) = agg_num_levels;
                                                                                                                  
   return hypre_error_flag;
}
                                                                                                                  
/*--------------------------------------------------------------------------
 * hypre_AMGHybridSetNumFunctions
 *--------------------------------------------------------------------------*/
                                                                                                                  
int
hypre_AMGHybridSetNumFunctions( void   *AMGhybrid_vdata,
                              int    num_functions      )
{
   hypre_AMGHybridData *AMGhybrid_data = AMGhybrid_vdata;
   if (!AMGhybrid_data)
   {
      hypre_error_in_arg(1);
      return hypre_error_flag;
   }
   if (num_functions < 1)
   {
      hypre_error_in_arg(2);
      return hypre_error_flag;
   }
                                                                                                                  
   (AMGhybrid_data -> num_functions) = num_functions;
                                                                                                                  
   return hypre_error_flag;
}
                                                                                                                  
/*--------------------------------------------------------------------------
 * hypre_AMGHybridSetNodal
 *--------------------------------------------------------------------------*/
                                                                                                                  
int
hypre_AMGHybridSetNodal( void   *AMGhybrid_vdata,
                              int    nodal      )
{
   hypre_AMGHybridData *AMGhybrid_data = AMGhybrid_vdata;
   if (!AMGhybrid_data)
   {
      hypre_error_in_arg(1);
      return hypre_error_flag;
   }
                                                                                                                  
   (AMGhybrid_data -> nodal) = nodal;
                                                                                                                  
   return hypre_error_flag;
}
                                                                                                                  
/*--------------------------------------------------------------------------
 * hypre_AMGHybridGetNumIterations
 *--------------------------------------------------------------------------*/

int
hypre_AMGHybridGetNumIterations( void   *AMGhybrid_vdata,
                              int    *num_its      )
{
   hypre_AMGHybridData *AMGhybrid_data = AMGhybrid_vdata;
   if (!AMGhybrid_data)
   {
      hypre_error_in_arg(1);
      return hypre_error_flag;
   }

   *num_its = (AMGhybrid_data -> dscg_num_its) + (AMGhybrid_data -> pcg_num_its);

   return hypre_error_flag;
}

/*--------------------------------------------------------------------------
 * hypre_AMGHybridGetDSCGNumIterations
 *--------------------------------------------------------------------------*/

int
hypre_AMGHybridGetDSCGNumIterations( void   *AMGhybrid_vdata,
                                  int    *dscg_num_its )
{
   hypre_AMGHybridData *AMGhybrid_data = AMGhybrid_vdata;
   if (!AMGhybrid_data)
   {
      hypre_error_in_arg(1);
      return hypre_error_flag;
   }

   *dscg_num_its = (AMGhybrid_data -> dscg_num_its);

   return hypre_error_flag;
}

/*--------------------------------------------------------------------------
 * hypre_AMGHybridGetPCGNumIterations
 *--------------------------------------------------------------------------*/

int
hypre_AMGHybridGetPCGNumIterations( void   *AMGhybrid_vdata,
                                 int    *pcg_num_its  )
{
   hypre_AMGHybridData *AMGhybrid_data = AMGhybrid_vdata;
   if (!AMGhybrid_data)
   {
      hypre_error_in_arg(1);
      return hypre_error_flag;
   }

   *pcg_num_its = (AMGhybrid_data -> pcg_num_its);

   return hypre_error_flag;
}

/*--------------------------------------------------------------------------
 * hypre_AMGHybridGetFinalRelativeResidualNorm
 *--------------------------------------------------------------------------*/

int
hypre_AMGHybridGetFinalRelativeResidualNorm( void   *AMGhybrid_vdata,
                                          double *final_rel_res_norm )
{
   hypre_AMGHybridData *AMGhybrid_data = AMGhybrid_vdata;
   if (!AMGhybrid_data)
   {
      hypre_error_in_arg(1);
      return hypre_error_flag;
   }

   *final_rel_res_norm = (AMGhybrid_data -> final_rel_res_norm);

   return hypre_error_flag;
}

/*--------------------------------------------------------------------------
 * hypre_AMGHybridSetup
 *--------------------------------------------------------------------------*/

int
hypre_AMGHybridSetup( void               *AMGhybrid_vdata,
                   hypre_ParCSRMatrix *A,
                   hypre_ParVector *b,
                   hypre_ParVector *x            )
{
   hypre_AMGHybridData *AMGhybrid_data = AMGhybrid_vdata;
   if (!AMGhybrid_data)
   {
      hypre_error_in_arg(1);
      return hypre_error_flag;
   }

   return hypre_error_flag;
}

/*--------------------------------------------------------------------------
 * hypre_AMGHybridSolve
 *--------------------------------------------------------------------------
 *
 * This solver is designed to solve Ax=b using a AMGhybrid algorithm. First
 * the solver uses diagonally scaled conjugate gradients. If sufficient
 * progress is not made, the algorithm switches to preconditioned
 * conjugate gradients with user-specified preconditioner.
 *
 *--------------------------------------------------------------------------*/

int
hypre_AMGHybridSolve( void               *AMGhybrid_vdata,
                   hypre_ParCSRMatrix *A,
                   hypre_ParVector *b,
                   hypre_ParVector *x            )
{
   hypre_AMGHybridData  *AMGhybrid_data    = AMGhybrid_vdata;

   double             tol;
   double             a_tol;
   double             cf_tol;
   int                dscg_max_its;
   int                pcg_max_its;
   int                two_norm;
   int                stop_crit;
   int                rel_change;
   int                logging;
   int                print_level;
   int                setup_type;
   int                solver_type;
   int                k_dim;
   /* BoomerAMG info */
   double 	strong_threshold;
   double      	max_row_sum;
   double	trunc_factor;
   int          pmax;
   int		max_levels;
   int		measure_type;
   int		coarsen_type;
   int		interp_type;
   int		cycle_type;
   int		num_paths;
   int		agg_num_levels;
   int		num_functions;
   int		nodal;
   int	       *num_grid_sweeps;
   int	       *grid_relax_type;
   int	      **grid_relax_points;
   double      *relax_weight;
   double      *omega;
   int         *dof_func;

   int	       *boom_ngs;
   int	       *boom_grt;
   int         *boom_dof_func;
   int	      **boom_grp;
   double      *boom_rlxw;
   double      *boom_omega;

   int                pcg_default;
   int              (/*__stdcall*/ *pcg_precond_solve)();
   int              (/*__stdcall*/ *pcg_precond_setup)();
   void              *pcg_precond;

   void              *pcg_solver;
   hypre_PCGFunctions *pcg_functions;
   hypre_GMRESFunctions *gmres_functions;
   hypre_BiCGSTABFunctions *bicgstab_functions;
                                                                                                                                        
   int                dscg_num_its=0;
   int                pcg_num_its=0;
   int                converged=0;
   int                num_variables = hypre_VectorSize(hypre_ParVectorLocalVector(b));
   double             res_norm;

   int                i, j;
   int		      sol_print_level; /* print_level for solver */
   int		      pre_print_level; /* print_level for preconditioner */

   if (!AMGhybrid_data)
   {
      hypre_error_in_arg(1);
      return hypre_error_flag;
   }
   /*-----------------------------------------------------------------------
    * Setup diagonal scaled solver
    *-----------------------------------------------------------------------*/

   if (!AMGhybrid_data)
   {
      hypre_error_in_arg(1);
      return hypre_error_flag;
   }
   tol            = (AMGhybrid_data -> tol);
   a_tol          = (AMGhybrid_data -> a_tol);
   cf_tol         = (AMGhybrid_data -> cf_tol);
   dscg_max_its   = (AMGhybrid_data -> dscg_max_its);
   pcg_max_its    = (AMGhybrid_data -> pcg_max_its);
   two_norm       = (AMGhybrid_data -> two_norm);
   stop_crit      = (AMGhybrid_data -> stop_crit);
   rel_change     = (AMGhybrid_data -> rel_change);
   logging        = (AMGhybrid_data -> logging);
   print_level    = (AMGhybrid_data -> print_level);
   setup_type     = (AMGhybrid_data -> setup_type);
   solver_type    = (AMGhybrid_data -> solver_type);
   k_dim          = (AMGhybrid_data -> k_dim);
   strong_threshold = (AMGhybrid_data -> strong_threshold);
   max_row_sum = (AMGhybrid_data -> max_row_sum);
   trunc_factor = (AMGhybrid_data -> trunc_factor);
   pmax = (AMGhybrid_data -> pmax);
   max_levels = (AMGhybrid_data -> max_levels);
   measure_type = (AMGhybrid_data -> measure_type);
   coarsen_type = (AMGhybrid_data -> coarsen_type);
   interp_type = (AMGhybrid_data -> interp_type);
   cycle_type = (AMGhybrid_data -> cycle_type);
   num_paths = (AMGhybrid_data -> num_paths);
   agg_num_levels = (AMGhybrid_data -> agg_num_levels);
   num_functions = (AMGhybrid_data -> num_functions);
   nodal = (AMGhybrid_data -> nodal);
   num_grid_sweeps = (AMGhybrid_data -> num_grid_sweeps);
   grid_relax_type = (AMGhybrid_data -> grid_relax_type);
   grid_relax_points = (AMGhybrid_data -> grid_relax_points);
   relax_weight = (AMGhybrid_data -> relax_weight);
   omega = (AMGhybrid_data -> omega);
   dof_func = (AMGhybrid_data -> dof_func);
   pcg_default    = (AMGhybrid_data -> pcg_default);
   if (!b)
   {
      hypre_error_in_arg(3);
      return hypre_error_flag;
   }
   num_variables = hypre_VectorSize(hypre_ParVectorLocalVector(b));
   if (!A)
   {
      hypre_error_in_arg(2);
      return hypre_error_flag;
   }
   if (!x)
   {
      hypre_error_in_arg(4);
      return hypre_error_flag;
   }
  
/*  print_level definitions: xy,  sol_print_level = y, pre_print_level = x */
   pre_print_level = print_level/10;
   sol_print_level = print_level - pre_print_level*10;
  
   pcg_solver = (AMGhybrid_data -> pcg_solver);
   pcg_precond = (AMGhybrid_data -> pcg_precond);
   (AMGhybrid_data -> dscg_num_its) = 0;
   (AMGhybrid_data -> pcg_num_its) = 0;
                                                                                                                                        
   if (setup_type || pcg_precond == NULL)
   {
    if (pcg_precond)
    {
       hypre_BoomerAMGDestroy(pcg_precond);
       pcg_precond = NULL;
       (AMGhybrid_data -> pcg_precond) = NULL;
    }
    if (solver_type == 1)
    {
     if (pcg_solver == NULL)
     {
      pcg_functions =
      hypre_PCGFunctionsCreate(
         hypre_CAlloc, hypre_ParKrylovFree,
         hypre_ParKrylovCommInfo,
         hypre_ParKrylovCreateVector,
         hypre_ParKrylovDestroyVector, hypre_ParKrylovMatvecCreate,
         hypre_ParKrylovMatvec,
         hypre_ParKrylovMatvecDestroy,
         hypre_ParKrylovInnerProd, hypre_ParKrylovCopyVector,
         hypre_ParKrylovClearVector,
         hypre_ParKrylovScaleVector, hypre_ParKrylovAxpy,
         hypre_ParKrylovIdentitySetup, hypre_ParKrylovIdentity );
      pcg_solver = hypre_PCGCreate( pcg_functions );
                                                                                                                                        
      hypre_PCGSetMaxIter(pcg_solver, dscg_max_its);
      hypre_PCGSetTol(pcg_solver, tol);
      hypre_PCGSetAbsoluteTol(pcg_solver, a_tol);
      hypre_PCGSetTwoNorm(pcg_solver, two_norm);
      hypre_PCGSetStopCrit(pcg_solver, stop_crit);
      hypre_PCGSetRelChange(pcg_solver, rel_change);
      hypre_PCGSetLogging(pcg_solver, logging);
      hypre_PCGSetPrintLevel(pcg_solver, sol_print_level);
                                                                                                                                        
      pcg_precond = NULL;
     }
                                                                                                                                        
     hypre_PCGSetConvergenceFactorTol(pcg_solver, cf_tol);
     hypre_PCGSetPrecond(pcg_solver,
                       HYPRE_ParCSRDiagScale,
                       HYPRE_ParCSRDiagScaleSetup,
                       pcg_precond);
     hypre_PCGSetup(pcg_solver, (void*) A, (void*) b, (void*) x);
     (AMGhybrid_data -> pcg_solver) = pcg_solver;

     /*---------------------------------------------------------------------
      * Solve with DSCG.
      *---------------------------------------------------------------------*/
     hypre_PCGSolve(pcg_solver, (void*) A, (void*) b, (void*) x, NULL);

     /*---------------------------------------------------------------------
      * Get information for DSCG.
      *---------------------------------------------------------------------*/
     hypre_PCGGetNumIterations(pcg_solver, &dscg_num_its);
     (AMGhybrid_data -> dscg_num_its) = dscg_num_its;
     hypre_PCGGetFinalRelativeResidualNorm(pcg_solver, &res_norm);

     hypre_PCGGetConverged(pcg_solver, &converged);

    }
    else if (solver_type == 2)
    {
     if (pcg_solver == NULL)
     {
      gmres_functions =
      hypre_GMRESFunctionsCreate(
         hypre_CAlloc, hypre_ParKrylovFree,
         hypre_ParKrylovCommInfo,
         hypre_ParKrylovCreateVector,
         hypre_ParKrylovCreateVectorArray,
         hypre_ParKrylovDestroyVector, hypre_ParKrylovMatvecCreate,
         hypre_ParKrylovMatvec,
         hypre_ParKrylovMatvecDestroy,
         hypre_ParKrylovInnerProd, hypre_ParKrylovCopyVector,
         hypre_ParKrylovClearVector,
         hypre_ParKrylovScaleVector, hypre_ParKrylovAxpy,
         hypre_ParKrylovIdentitySetup, hypre_ParKrylovIdentity );
      pcg_solver = hypre_GMRESCreate( gmres_functions );
                                                                                                                                        
      hypre_GMRESSetMaxIter(pcg_solver, dscg_max_its);
      hypre_GMRESSetTol(pcg_solver, tol);
      hypre_GMRESSetAbsoluteTol(pcg_solver, a_tol);
      hypre_GMRESSetKDim(pcg_solver, k_dim);
      hypre_GMRESSetStopCrit(pcg_solver, stop_crit);
      hypre_GMRESSetRelChange(pcg_solver, rel_change);
      hypre_GMRESSetLogging(pcg_solver, logging);
      hypre_GMRESSetPrintLevel(pcg_solver, sol_print_level);
                                                                                                                                        
      pcg_precond = NULL;
     }
                                                                                                                                        
     hypre_GMRESSetConvergenceFactorTol(pcg_solver, cf_tol);
     hypre_GMRESSetPrecond(pcg_solver,
                       HYPRE_ParCSRDiagScale,
                       HYPRE_ParCSRDiagScaleSetup,
                       pcg_precond);
     hypre_GMRESSetup(pcg_solver, (void*) A, (void*) b, (void*) x);
     (AMGhybrid_data -> pcg_solver) = pcg_solver;

     /*---------------------------------------------------------------------
      * Solve with diagonal scaled GMRES
      *---------------------------------------------------------------------*/
      hypre_GMRESSolve(pcg_solver, (void*) A, (void*) b, (void*) x);

     /*---------------------------------------------------------------------
      * Get information for GMRES
      *---------------------------------------------------------------------*/
      hypre_GMRESGetNumIterations(pcg_solver, &dscg_num_its);
      (AMGhybrid_data -> dscg_num_its) = dscg_num_its;
      hypre_GMRESGetFinalRelativeResidualNorm(pcg_solver, &res_norm);

      hypre_GMRESGetConverged(pcg_solver, &converged);

    }
    else if (solver_type == 3)
    {
     if (pcg_solver == NULL)
     {
      bicgstab_functions =
      hypre_BiCGSTABFunctionsCreate(
         hypre_ParKrylovCreateVector,
         hypre_ParKrylovDestroyVector, hypre_ParKrylovMatvecCreate,
         hypre_ParKrylovMatvec,
         hypre_ParKrylovMatvecDestroy,
         hypre_ParKrylovInnerProd, hypre_ParKrylovCopyVector,
         hypre_ParKrylovClearVector,
         hypre_ParKrylovScaleVector, hypre_ParKrylovAxpy,
         hypre_ParKrylovCommInfo,
         hypre_ParKrylovIdentitySetup, hypre_ParKrylovIdentity );
      pcg_solver = hypre_BiCGSTABCreate( bicgstab_functions );
                                                                                                                                        
      hypre_BiCGSTABSetMaxIter(pcg_solver, dscg_max_its);
      hypre_BiCGSTABSetTol(pcg_solver, tol);
      hypre_BiCGSTABSetAbsoluteTol(pcg_solver, a_tol);
      hypre_BiCGSTABSetStopCrit(pcg_solver, stop_crit);
      hypre_BiCGSTABSetLogging(pcg_solver, logging);
      hypre_BiCGSTABSetPrintLevel(pcg_solver, sol_print_level);
                                                                                                                                        
      pcg_precond = NULL;
     }
                                                                                                                                        
     hypre_BiCGSTABSetConvergenceFactorTol(pcg_solver, cf_tol);
     hypre_BiCGSTABSetPrecond(pcg_solver,
                       HYPRE_ParCSRDiagScale,
                       HYPRE_ParCSRDiagScaleSetup,
                       pcg_precond);
     hypre_BiCGSTABSetup(pcg_solver, (void*) A, (void*) b, (void*) x);
     (AMGhybrid_data -> pcg_solver) = pcg_solver;
                                                                                                                                        
     /*---------------------------------------------------------------------
      * Solve with diagonal scaled BiCGSTAB
      *---------------------------------------------------------------------*/
      hypre_BiCGSTABSolve(pcg_solver, (void*) A, (void*) b, (void*) x);

     /*---------------------------------------------------------------------
      * Get information for BiCGSTAB
      *---------------------------------------------------------------------*/
     hypre_BiCGSTABGetNumIterations(pcg_solver, &dscg_num_its);
     (AMGhybrid_data -> dscg_num_its) = dscg_num_its;
     hypre_BiCGSTABGetFinalRelativeResidualNorm(pcg_solver, &res_norm);

     hypre_BiCGSTABGetConverged(pcg_solver, &converged);

    }
   }

   /*---------------------------------------------------------------------
    * If converged, done... 
    *---------------------------------------------------------------------*/
   if (converged)
   {
      if (logging)
         (AMGhybrid_data -> final_rel_res_norm) = res_norm;
   }
   /*-----------------------------------------------------------------------
    * ... otherwise, use AMG+solver
    *-----------------------------------------------------------------------*/
   else
   {
      /*--------------------------------------------------------------------
       * Free up previous PCG solver structure and set up a new one.
       *--------------------------------------------------------------------*/
      if (solver_type == 1)
      {
         hypre_PCGSetMaxIter(pcg_solver, pcg_max_its);
         hypre_PCGSetConvergenceFactorTol(pcg_solver, 0.0);
      }
      else if (solver_type == 2)
      {
         hypre_GMRESSetMaxIter(pcg_solver, pcg_max_its);
         hypre_GMRESSetConvergenceFactorTol(pcg_solver, 0.0);
      }
      else if (solver_type == 3)
      {
         hypre_BiCGSTABSetMaxIter(pcg_solver, pcg_max_its);
         hypre_BiCGSTABSetConvergenceFactorTol(pcg_solver, 0.0);
      }

      /* Setup preconditioner */
      if (setup_type && pcg_default)
      {
         pcg_precond = hypre_BoomerAMGCreate();
         hypre_BoomerAMGSetMaxIter(pcg_precond, 1);
         hypre_BoomerAMGSetTol(pcg_precond, 0.0);
         hypre_BoomerAMGSetCoarsenType(pcg_precond, coarsen_type);
         hypre_BoomerAMGSetInterpType(pcg_precond, interp_type);
         hypre_BoomerAMGSetSetupType(pcg_precond, setup_type);
         hypre_BoomerAMGSetMeasureType(pcg_precond, measure_type);
         hypre_BoomerAMGSetStrongThreshold(pcg_precond, strong_threshold);
         hypre_BoomerAMGSetTruncFactor(pcg_precond, trunc_factor);
         hypre_BoomerAMGSetPMaxElmts(pcg_precond, pmax);
         hypre_BoomerAMGSetCycleType(pcg_precond, cycle_type);
         hypre_BoomerAMGSetPrintLevel(pcg_precond, pre_print_level);
         hypre_BoomerAMGSetMaxLevels(pcg_precond,  max_levels);
         hypre_BoomerAMGSetMaxRowSum(pcg_precond, max_row_sum);
         hypre_BoomerAMGSetAggNumLevels(pcg_precond, agg_num_levels);
         hypre_BoomerAMGSetNumPaths(pcg_precond, num_paths);
         hypre_BoomerAMGSetNumFunctions(pcg_precond, num_functions);
         hypre_BoomerAMGSetNodal(pcg_precond, nodal);
   	 if (num_grid_sweeps)
         {	
	    boom_ngs = hypre_CTAlloc(int,4);
	    for (i=0; i < 4; i++)
	       boom_ngs[i] = num_grid_sweeps[i];
            hypre_BoomerAMGSetNumGridSweeps(pcg_precond, boom_ngs);
         }
   	 if (grid_relax_type)
         {
	    boom_grt = hypre_CTAlloc(int,4);
	    for (i=0; i < 4; i++)
	       boom_grt[i] = grid_relax_type[i];
	    if (solver_type == 1 && grid_relax_type[1] == 3 &&
		grid_relax_type[2] == 3)
	       boom_grt[2] = 4;
   	    hypre_BoomerAMGSetGridRelaxType(pcg_precond, boom_grt);
         }
   	 if (relax_weight)
         {
	    boom_rlxw = hypre_CTAlloc(double,max_levels);
	    for (i=0; i < max_levels; i++)
	       boom_rlxw[i] = relax_weight[i];
            hypre_BoomerAMGSetRelaxWeight(pcg_precond, boom_rlxw);
         }
   	 if (omega)
         {
	    boom_omega = hypre_CTAlloc(double,max_levels);
	    for (i=0; i < max_levels; i++)
	       boom_omega[i] = omega[i];
            hypre_BoomerAMGSetOmega(pcg_precond, boom_omega);
         }
   	 if (grid_relax_points)
         {
	    boom_grp = hypre_CTAlloc(int*,4);
	    for (i=0; i < 4; i++)
 	    {
	       boom_grp[i] = hypre_CTAlloc(int, num_grid_sweeps[i]);
	       for (j=0; j < num_grid_sweeps[i]; j++)
		  boom_grp[i][j] = grid_relax_points[i][j];
    	    }
            hypre_BoomerAMGSetGridRelaxPoints(pcg_precond, boom_grp);
         }
   	 if (dof_func)
         {
	    boom_dof_func = hypre_CTAlloc(int,num_variables);
	    for (i=0; i < num_variables; i++)
	       boom_dof_func[i] = dof_func[i];
            hypre_BoomerAMGSetDofFunc(pcg_precond, boom_dof_func);
         }
         pcg_precond_solve = HYPRE_BoomerAMGSolve;
         pcg_precond_setup = HYPRE_BoomerAMGSetup;
         (AMGhybrid_data -> pcg_precond_setup) = pcg_precond_setup;
         (AMGhybrid_data -> pcg_precond_solve) = pcg_precond_solve;
         (AMGhybrid_data -> pcg_precond) = pcg_precond;
         /*(AMGhybrid_data -> pcg_default) = 0;*/
         /*(AMGhybrid_data -> setup_type) = 0;*/
      }
      else
      {
         pcg_precond       = (AMGhybrid_data -> pcg_precond);
         pcg_precond_solve = (AMGhybrid_data -> pcg_precond_solve);
         pcg_precond_setup = (AMGhybrid_data -> pcg_precond_setup);
         hypre_BoomerAMGSetSetupType(pcg_precond, setup_type);
      }

      /* Complete setup of solver+AMG */
      if (solver_type == 1)
      {
         hypre_PCGSetPrecond(pcg_solver,
                          pcg_precond_solve, pcg_precond_setup, pcg_precond);
         hypre_PCGSetup(pcg_solver, (void*) A, (void*) b, (void*) x);

         /* Solve */
         hypre_PCGSolve(pcg_solver, (void*) A, (void*) b, (void*) x, NULL);

         /* Get information from PCG that is always logged in AMGhybrid solver*/
         hypre_PCGGetNumIterations(pcg_solver, &pcg_num_its);
         (AMGhybrid_data -> pcg_num_its)  = pcg_num_its;
         if (logging)
         {
            hypre_PCGGetFinalRelativeResidualNorm(pcg_solver, &res_norm);
            (AMGhybrid_data -> final_rel_res_norm) = res_norm;
         }
      }
      else if (solver_type == 2)
      {
         hypre_GMRESSetPrecond(pcg_solver,
                          pcg_precond_solve, pcg_precond_setup, pcg_precond);
         hypre_GMRESSetup(pcg_solver, (void*) A, (void*) b, (void*) x);

         /* Solve */
         hypre_GMRESSolve(pcg_solver, (void*) A, (void*) b, (void*) x);

         /* Get information from GMRES that is always logged in AMGhybrid solver*/
         hypre_GMRESGetNumIterations(pcg_solver, &pcg_num_its);
         (AMGhybrid_data -> pcg_num_its)  = pcg_num_its;
         if (logging)
         {
            hypre_GMRESGetFinalRelativeResidualNorm(pcg_solver, &res_norm);
            (AMGhybrid_data -> final_rel_res_norm) = res_norm;
         }
      }
      else if (solver_type == 3)
      {
         hypre_BiCGSTABSetPrecond(pcg_solver,
                          pcg_precond_solve, pcg_precond_setup, pcg_precond);
         hypre_BiCGSTABSetup(pcg_solver, (void*) A, (void*) b, (void*) x);

         /* Solve */
         hypre_BiCGSTABSolve(pcg_solver, (void*) A, (void*) b, (void*) x);

         /* Get information from BiCGSTAB that is always logged in AMGhybrid solver*/
         hypre_BiCGSTABGetNumIterations(pcg_solver, &pcg_num_its);
         (AMGhybrid_data -> pcg_num_its)  = pcg_num_its;
         if (logging)
         {
	    hypre_BiCGSTABGetFinalRelativeResidualNorm(pcg_solver, &res_norm);
            (AMGhybrid_data -> final_rel_res_norm) = res_norm;
         }
      }
   }

   return hypre_error_flag;
}

