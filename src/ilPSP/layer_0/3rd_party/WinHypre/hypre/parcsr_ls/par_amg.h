/*BHEADER**********************************************************************
 * Copyright (c) 2008,  Lawrence Livermore National Security, LLC.
 * Produced at the Lawrence Livermore National Laboratory.
 * This file is part of HYPRE.  See file COPYRIGHT for details.
 *
 * HYPRE is free software; you can redistribute it and/or modify it under the
 * terms of the GNU Lesser General Public License (as published by the Free
 * Software Foundation) version 2.1 dated February 1999.
 *
 * $Revision: 2.35 $
 ***********************************************************************EHEADER*/





#ifndef hypre_ParAMG_DATA_HEADER
#define hypre_ParAMG_DATA_HEADER

#define CUMNUMIT

#include "../parcsr_block_mv/par_csr_block_matrix.h"


/*--------------------------------------------------------------------------
 * hypre_ParAMGData
 *--------------------------------------------------------------------------*/

typedef struct
{

   /* setup params */
   int      max_levels;
   double   strong_threshold;
   double   max_row_sum;
   double   trunc_factor;
   double   jacobi_trunc_threshold;
   double   S_commpkg_switch;
   double   CR_rate;
   double   CR_strong_th;
   int      measure_type;
   int      setup_type;
   int      coarsen_type;
   int      P_max_elmts;
   int      interp_type;
   int      restr_par;
   int      agg_num_levels;
   int      num_paths;
   int      post_interp_type;
   int      num_CR_relax_steps;
   int      IS_type;
   int      CR_use_CG;
   int      cgc_its;

   /* solve params */
   int      max_iter;
   int      min_iter;
   int      cycle_type;    
   int     *num_grid_sweeps;  
   int     *grid_relax_type;   
   int    **grid_relax_points;
   int      relax_order;
   int      user_coarse_relax_type;   
   double  *relax_weight; 
   double  *omega;
   double   tol;

   /* problem data */
   hypre_ParCSRMatrix  *A;
   int      num_variables;
   int      num_functions;
   int      nodal;
   int      nodal_diag;
   int      num_points;
   int     *dof_func;
   int     *dof_point;           
   int     *point_dof_map;

   /* data generated in the setup phase */
   hypre_ParCSRMatrix **A_array;
   hypre_ParVector    **F_array;
   hypre_ParVector    **U_array;
   hypre_ParCSRMatrix **P_array;
   hypre_ParCSRMatrix **R_array;
   int                **CF_marker_array;
   int                **dof_func_array;
   int                **dof_point_array;
   int                **point_dof_map_array;
   int                  num_levels;
   double             **l1_norms;


   /* Block data */
   hypre_ParCSRBlockMatrix **A_block_array;
   hypre_ParCSRBlockMatrix **P_block_array;
   hypre_ParCSRBlockMatrix **R_block_array;

   int block_mode;

   /* data for more complex smoothers */
   int                  smooth_num_levels;
   int                  smooth_type;
   HYPRE_Solver        *smoother;
   int			smooth_num_sweeps;
   int                  schw_variant;
   int                  schw_overlap;
   int                  schw_domain_type;
   double		schwarz_rlx_weight;
   int                  schwarz_use_nonsymm;
   int			ps_sym;
   int			ps_level;
   int			pi_max_nz_per_row;
   int			eu_level;
   int			eu_bj;
   double		ps_threshold;
   double		ps_filter;
   double		pi_drop_tol;
   double		eu_sparse_A;
   char		       *euclidfile;

   /* data generated in the solve phase */
   hypre_ParVector   *Vtemp;
   hypre_Vector      *Vtemp_local;
   double            *Vtemp_local_data;
   double             cycle_op_count;
   hypre_ParVector   *Rtemp;
   hypre_ParVector   *Ptemp;
   hypre_ParVector   *Ztemp;

   /* fields used by GSMG and LS interpolation */
   int                 gsmg;        /* nonzero indicates use of GSMG */
   int                 num_samples; /* number of sample vectors */

   /* log info */
   int      logging;
   int      num_iterations;
#ifdef CUMNUMIT
   int      cum_num_iterations;
#endif
   double   rel_resid_norm;
   hypre_ParVector *residual; /* available if logging>1 */

   /* output params */
   int      print_level;
   char     log_file_name[256];
   int      debug_flag;

   /* whether to print the constructed coarse grids BM Oct 22, 2006 */
   int      plot_grids;
   char     plot_filename[251];

   /* coordinate data BM Oct 17, 2006 */
   int      coorddim;
   float    *coordinates;

} hypre_ParAMGData;

/*--------------------------------------------------------------------------
 * Accessor functions for the hypre_AMGData structure
 *--------------------------------------------------------------------------*/

/* setup params */
		  		      
#define hypre_ParAMGDataRestriction(amg_data) ((amg_data)->restr_par)
#define hypre_ParAMGDataMaxLevels(amg_data) ((amg_data)->max_levels)
#define hypre_ParAMGDataStrongThreshold(amg_data) \
((amg_data)->strong_threshold)
#define hypre_ParAMGDataMaxRowSum(amg_data) ((amg_data)->max_row_sum)
#define hypre_ParAMGDataTruncFactor(amg_data) ((amg_data)->trunc_factor)
#define hypre_ParAMGDataJacobiTruncThreshold(amg_data) ((amg_data)->jacobi_trunc_threshold)
#define hypre_ParAMGDataSCommPkgSwitch(amg_data) ((amg_data)->S_commpkg_switch)
#define hypre_ParAMGDataInterpType(amg_data) ((amg_data)->interp_type)
#define hypre_ParAMGDataCoarsenType(amg_data) ((amg_data)->coarsen_type)
#define hypre_ParAMGDataMeasureType(amg_data) ((amg_data)->measure_type)
#define hypre_ParAMGDataSetupType(amg_data) ((amg_data)->setup_type)
#define hypre_ParAMGDataPMaxElmts(amg_data) ((amg_data)->P_max_elmts)
#define hypre_ParAMGDataNumPaths(amg_data) ((amg_data)->num_paths)
#define hypre_ParAMGDataAggNumLevels(amg_data) ((amg_data)->agg_num_levels)
#define hypre_ParAMGDataPostInterpType(amg_data) ((amg_data)->post_interp_type)
#define hypre_ParAMGDataNumCRRelaxSteps(amg_data) ((amg_data)->num_CR_relax_steps)
#define hypre_ParAMGDataCRRate(amg_data) ((amg_data)->CR_rate)
#define hypre_ParAMGDataCRStrongTh(amg_data) ((amg_data)->CR_strong_th)
#define hypre_ParAMGDataISType(amg_data) ((amg_data)->IS_type)
#define hypre_ParAMGDataCRUseCG(amg_data) ((amg_data)->CR_use_CG)
#define hypre_ParAMGDataL1Norms(amg_data) ((amg_data)->l1_norms)
 #define hypre_ParAMGDataCGCIts(amg_data) ((amg_data)->cgc_its)

/* solve params */

#define hypre_ParAMGDataMinIter(amg_data) ((amg_data)->min_iter)
#define hypre_ParAMGDataMaxIter(amg_data) ((amg_data)->max_iter)
#define hypre_ParAMGDataCycleType(amg_data) ((amg_data)->cycle_type)
#define hypre_ParAMGDataTol(amg_data) ((amg_data)->tol)
#define hypre_ParAMGDataNumGridSweeps(amg_data) ((amg_data)->num_grid_sweeps)
#define hypre_ParAMGDataUserCoarseRelaxType(amg_data) ((amg_data)->user_coarse_relax_type)
#define hypre_ParAMGDataGridRelaxType(amg_data) ((amg_data)->grid_relax_type)
#define hypre_ParAMGDataGridRelaxPoints(amg_data) \
((amg_data)->grid_relax_points)
#define hypre_ParAMGDataRelaxOrder(amg_data) ((amg_data)->relax_order)
#define hypre_ParAMGDataRelaxWeight(amg_data) ((amg_data)->relax_weight)
#define hypre_ParAMGDataOmega(amg_data) ((amg_data)->omega)

/* problem data parameters */
#define  hypre_ParAMGDataNumVariables(amg_data)  ((amg_data)->num_variables)
#define hypre_ParAMGDataNumFunctions(amg_data) ((amg_data)->num_functions)
#define hypre_ParAMGDataNodal(amg_data) ((amg_data)->nodal)
#define hypre_ParAMGDataNodalDiag(amg_data) ((amg_data)->nodal_diag)
#define hypre_ParAMGDataNumPoints(amg_data) ((amg_data)->num_points)
#define hypre_ParAMGDataDofFunc(amg_data) ((amg_data)->dof_func)
#define hypre_ParAMGDataDofPoint(amg_data) ((amg_data)->dof_point)
#define hypre_ParAMGDataPointDofMap(amg_data) ((amg_data)->point_dof_map)

/* data generated by the setup phase */
#define hypre_ParAMGDataCFMarkerArray(amg_data) ((amg_data)-> CF_marker_array)
#define hypre_ParAMGDataAArray(amg_data) ((amg_data)->A_array)
#define hypre_ParAMGDataFArray(amg_data) ((amg_data)->F_array)
#define hypre_ParAMGDataUArray(amg_data) ((amg_data)->U_array)
#define hypre_ParAMGDataPArray(amg_data) ((amg_data)->P_array)
#define hypre_ParAMGDataRArray(amg_data) ((amg_data)->R_array)
#define hypre_ParAMGDataDofFuncArray(amg_data) ((amg_data)->dof_func_array)
#define hypre_ParAMGDataDofPointArray(amg_data) ((amg_data)->dof_point_array)
#define hypre_ParAMGDataPointDofMapArray(amg_data) \
((amg_data)->point_dof_map_array) 
#define hypre_ParAMGDataNumLevels(amg_data) ((amg_data)->num_levels)	
#define hypre_ParAMGDataSmoothType(amg_data) ((amg_data)->smooth_type)
#define hypre_ParAMGDataSmoothNumLevels(amg_data) \
((amg_data)->smooth_num_levels)
#define hypre_ParAMGDataSmoothNumSweeps(amg_data) \
((amg_data)->smooth_num_sweeps)	
#define hypre_ParAMGDataSmoother(amg_data) ((amg_data)->smoother)	
#define hypre_ParAMGDataVariant(amg_data) ((amg_data)->schw_variant)	
#define hypre_ParAMGDataOverlap(amg_data) ((amg_data)->schw_overlap)	
#define hypre_ParAMGDataDomainType(amg_data) ((amg_data)->schw_domain_type)	
#define hypre_ParAMGDataSchwarzRlxWeight(amg_data) \
((amg_data)->schwarz_rlx_weight)
#define hypre_ParAMGDataSchwarzUseNonSymm(amg_data) \
((amg_data)->schwarz_use_nonsymm)
#define hypre_ParAMGDataSym(amg_data) ((amg_data)->ps_sym)	
#define hypre_ParAMGDataLevel(amg_data) ((amg_data)->ps_level)	
#define hypre_ParAMGDataMaxNzPerRow(amg_data) ((amg_data)->pi_max_nz_per_row)
#define hypre_ParAMGDataThreshold(amg_data) ((amg_data)->ps_threshold)	
#define hypre_ParAMGDataFilter(amg_data) ((amg_data)->ps_filter)	
#define hypre_ParAMGDataDropTol(amg_data) ((amg_data)->pi_drop_tol)	
#define hypre_ParAMGDataEuclidFile(amg_data) ((amg_data)->euclidfile)	
#define hypre_ParAMGDataEuLevel(amg_data) ((amg_data)->eu_level)	
#define hypre_ParAMGDataEuSparseA(amg_data) ((amg_data)->eu_sparse_A)
#define hypre_ParAMGDataEuBJ(amg_data) ((amg_data)->eu_bj)

/* block */
#define hypre_ParAMGDataABlockArray(amg_data) ((amg_data)->A_block_array)
#define hypre_ParAMGDataPBlockArray(amg_data) ((amg_data)->P_block_array)
#define hypre_ParAMGDataRBlockArray(amg_data) ((amg_data)->R_block_array)

#define hypre_ParAMGDataBlockMode(amg_data) ((amg_data)->block_mode)


/* data generated in the solve phase */
#define hypre_ParAMGDataVtemp(amg_data) ((amg_data)->Vtemp)
#define hypre_ParAMGDataVtempLocal(amg_data) ((amg_data)->Vtemp_local)
#define hypre_ParAMGDataVtemplocalData(amg_data) ((amg_data)->Vtemp_local_data)
#define hypre_ParAMGDataCycleOpCount(amg_data) ((amg_data)->cycle_op_count)
#define hypre_ParAMGDataRtemp(amg_data) ((amg_data)->Rtemp)
#define hypre_ParAMGDataPtemp(amg_data) ((amg_data)->Ptemp)
#define hypre_ParAMGDataZtemp(amg_data) ((amg_data)->Ztemp)

/* fields used by GSMG */
#define hypre_ParAMGDataGSMG(amg_data) ((amg_data)->gsmg)
#define hypre_ParAMGDataNumSamples(amg_data) ((amg_data)->num_samples)

/* log info data */
#define hypre_ParAMGDataLogging(amg_data) ((amg_data)->logging)
#define hypre_ParAMGDataNumIterations(amg_data) ((amg_data)->num_iterations)
#ifdef CUMNUMIT
#define hypre_ParAMGDataCumNumIterations(amg_data) ((amg_data)->cum_num_iterations)
#endif
#define hypre_ParAMGDataRelativeResidualNorm(amg_data) ((amg_data)->rel_resid_norm)
#define hypre_ParAMGDataResidual(amg_data) ((amg_data)->residual)

/* output parameters */
#define hypre_ParAMGDataPrintLevel(amg_data) ((amg_data)->print_level)
#define hypre_ParAMGDataLogFileName(amg_data) ((amg_data)->log_file_name)
#define hypre_ParAMGDataDebugFlag(amg_data)   ((amg_data)->debug_flag)

/* BM Oct 22, 2006 */
#define hypre_ParAMGDataPlotGrids(amg_data) ((amg_data)->plot_grids)
#define hypre_ParAMGDataPlotFileName(amg_data) ((amg_data)->plot_filename)

/* coordinates BM Oct 17, 2006 */
#define hypre_ParAMGDataCoordDim(amg_data) ((amg_data)->coorddim)
#define hypre_ParAMGDataCoordinates(amg_data) ((amg_data)->coordinates)

#endif



