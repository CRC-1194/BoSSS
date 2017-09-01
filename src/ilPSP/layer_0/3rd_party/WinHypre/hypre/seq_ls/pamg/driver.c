/*BHEADER**********************************************************************
 * Copyright (c) 2008,  Lawrence Livermore National Security, LLC.
 * Produced at the Lawrence Livermore National Laboratory.
 * This file is part of HYPRE.  See file COPYRIGHT for details.
 *
 * HYPRE is free software; you can redistribute it and/or modify it under the
 * terms of the GNU Lesser General Public License (as published by the Free
 * Software Foundation) version 2.1 dated February 1999.
 *
 * $Revision: 2.13 $
 ***********************************************************************EHEADER*/

#include "headers.h"
#include "krylov.h"

/*--------------------------------------------------------------------------
 * Test driver for unstructured matrix interface (csr storage).
 * Do `driver -help' for usage info.
 *--------------------------------------------------------------------------*/



int SetSysVcoefValues(int num_fun, int nx, int ny, int nz, double vcx, double vcy, double vcz, int mtx_entry, double *values);

int
main( int   argc,
      char *argv[] )
{
   int                 arg_index;
/*   int                 time_index; */
   int                 print_usage;
   int                 build_matrix_type;
   int                 build_matrix_arg_index;
   int                 build_rhs_type;
   int                 build_rhs_arg_index;
   int                 build_funcs_type;
   int                 build_funcs_arg_index;
   int                *dof_func;
   int                 interp_type;
   int                 solver_id;
   int                 relax_default;
   int                 coarsen_type;
   int                 max_levels;
   int                 num_functions, num_fun;
   int                 num_relax_steps;
   int                 mode = 0;
   int use_block_flag;
   double	       norm;
   double	       tol;
   int                 i, j, k;
   int                 k_dim;
   int		       ierr = 0;
   int		       agg_levels = 0;
   int		       agg_coarsen_type;
   int		       agg_interp_type;
   int		       num_jacs = 1;

#if 0
   hypre_ParCSRMatrix *A;
   hypre_ParVector    *b;
   hypre_ParVector    *x;
#endif
   hypre_CSRMatrix    *A;
   hypre_Vector       *b;
   hypre_Vector       *x;

   HYPRE_Solver        amg_solver;
   HYPRE_Solver        pcg_solver;
   HYPRE_Solver        pcg_precond;

/*   int                 num_procs, myid;   */

#if 0
   int 		      *global_part;
#endif

      double   strong_threshold;
      double   A_trunc_factor = 0;
      double   P_trunc_factor = 0;
      int      A_max_elmts = 0;
      int      P_max_elmts = 0;
      int      cycle_type;
      int      ioutdat = 1;
      int      print_level = 3;
      int      num_iterations;  
      int      num_sweep;  
      int     *num_grid_sweeps;  
      int     *grid_relax_type;   
      int    **grid_relax_points; 
      double  *relax_weight;
      double   final_res_norm;
      int     *schwarz_option;
      int      schwarz_lev;
      int      print_matrix = 0;
      int      system_vcoef = 0;
      

   /*-----------------------------------------------------------
    * Initialize some stuff
    *-----------------------------------------------------------*/

   dof_func = NULL;

   /* Initialize MPI */
   /* MPI_Init(&argc, &argv); */

#if 0 
   MPI_Comm_size(MPI_COMM_WORLD, &num_procs );
   MPI_Comm_rank(MPI_COMM_WORLD, &myid );
#endif

/*   num_procs = 1; 
     myid = 0;         */
/*
   hypre_InitMemoryDebug(myid);
*/
   /*-----------------------------------------------------------
    * Set defaults
    *-----------------------------------------------------------*/
 
   build_matrix_type      = 1;
   build_matrix_arg_index = argc;
   build_rhs_type      = 0;
   build_rhs_arg_index = argc;
   coarsen_type      = 0;
   relax_default = 0;
   interp_type = 0;
   num_functions = 1;
   use_block_flag = 0;
   num_relax_steps = 1;
   build_funcs_type = 0;
   build_funcs_arg_index = argc;
   tol = 1.e-6;
   k_dim = 5;

   solver_id = 0;
   max_levels = 25;

   /*-----------------------------------------------------------
    * Parse command line
    *-----------------------------------------------------------*/
 
   print_usage = 0;
   arg_index = 1;

   while ( (arg_index < argc) && (!print_usage) )
   {
      if ( strcmp(argv[arg_index], "-fromfile") == 0 )
      {
         arg_index++;
         build_matrix_type      = 0;
         build_matrix_arg_index = arg_index;
      }
      else if ( strcmp(argv[arg_index], "-laplacian") == 0 )
      {
         arg_index++;
         build_matrix_type      = 1;
         build_matrix_arg_index = arg_index;
      }
      else if ( strcmp(argv[arg_index], "-stencil") == 0 )
      {
         arg_index++;
         build_matrix_type      = 2;
         build_matrix_arg_index = arg_index;
      }
      else if ( strcmp(argv[arg_index], "-9pt") == 0 )
      {
         arg_index++;
         build_matrix_type      = 3;
         build_matrix_arg_index = arg_index;
      }
      else if ( strcmp(argv[arg_index], "-27pt") == 0 )
      {
         arg_index++;
         build_matrix_type      = 4;
         build_matrix_arg_index = arg_index;
      }
      else if ( strcmp(argv[arg_index], "-difconv") == 0 )
      {
         arg_index++;
         build_matrix_type      = 5;
         build_matrix_arg_index = arg_index;
      }
      else if ( strcmp(argv[arg_index], "-solver") == 0 )
      {
         arg_index++;
         solver_id = atoi(argv[arg_index++]);
      }
      else if ( strcmp(argv[arg_index], "-rhsfromfile") == 0 )
      {
         arg_index++;
         build_rhs_type      = 1;
         build_rhs_arg_index = arg_index;
      }
      else if ( strcmp(argv[arg_index], "-rhsrand") == 0 )
      {
         arg_index++;
         build_rhs_type      = 3;
         build_rhs_arg_index = arg_index;
      }    
      else if ( strcmp(argv[arg_index], "-rhsiszero") == 0 )
      {
         arg_index++;
         build_rhs_type      = 5;
         build_rhs_arg_index = arg_index;
      }    
      else if ( strcmp(argv[arg_index], "-xisone") == 0 )
      {
         arg_index++;
         build_rhs_type      = 4;
         build_rhs_arg_index = arg_index;
      }    
      else if ( strcmp(argv[arg_index], "-cljp") == 0 )
      {
         arg_index++;
         coarsen_type = 0;
      }
      else if ( strcmp(argv[arg_index], "-ruge") == 0 )
      {
         arg_index++;
         coarsen_type = 1;
      }
      else if ( strcmp(argv[arg_index], "-a1") == 0 )
      {
         arg_index++;
         agg_coarsen_type = 11;
      }
      else if ( strcmp(argv[arg_index], "-a2") == 0 )
      {
         arg_index++;
         agg_coarsen_type = 9;
      }
      else if ( strcmp(argv[arg_index], "-cljp2") == 0 )
      {
         arg_index++;
         agg_coarsen_type = 8;
      }
      else if ( strcmp(argv[arg_index], "-cljp1") == 0 )
      {
         arg_index++;
         agg_coarsen_type = 10;
      }
      else if ( strcmp(argv[arg_index], "-agg") == 0 )
      {
         arg_index++;
         agg_levels = atoi(argv[arg_index++]);
      }
      else if ( strcmp(argv[arg_index], "-nj") == 0 )
      {
         arg_index++;
         num_jacs = atoi(argv[arg_index++]);
      }
      /* begin HANS added */
      else if ( strcmp(argv[arg_index], "-wLJP") == 0 )
      {
         arg_index++;
         agg_coarsen_type = 4;
      }
      else if ( strcmp(argv[arg_index], "-wLJP2") == 0 )
      {
         arg_index++;
         agg_coarsen_type = 12;
      }
      else if ( strcmp(argv[arg_index], "-ruge1p") == 0 )
      {
         arg_index++;
         coarsen_type = 5;
      }
      else if ( strcmp(argv[arg_index], "-mp") == 0 )
      {
         arg_index++;
         agg_interp_type = 5;
      }
      else if ( strcmp(argv[arg_index], "-jac") == 0 )
      {
         arg_index++;
         agg_interp_type = 6;
      }
      /* end HANS added */

      else if ( strcmp(argv[arg_index], "-rugeL") == 0 )
      {
         arg_index++;
         coarsen_type = 2;
      }
      else if ( strcmp(argv[arg_index], "-cr") == 0 )
      {
         arg_index++;
         coarsen_type = 3;
      }
      else if ( strcmp(argv[arg_index], "-tol") == 0 )
      {
         arg_index++;
         tol = atof(argv[arg_index++]);
      }
      else if ( strcmp(argv[arg_index], "-k") == 0 )
      {
         arg_index++;
         k_dim = atoi(argv[arg_index++]);
      }
      else if ( strcmp(argv[arg_index], "-nrlx") == 0 )
      {
         arg_index++;
         num_relax_steps = atoi(argv[arg_index++]);
      }
      else if ( strcmp(argv[arg_index], "-rlx") == 0 )
      {
         arg_index++;
         relax_default = atoi(argv[arg_index++]);
      }
      else if ( strcmp(argv[arg_index], "-maxlev") == 0 )
      {
         arg_index++;
         max_levels = atoi(argv[arg_index++]);
      }
      else if ( strcmp(argv[arg_index], "-pou") == 0 )
      {
         arg_index++;
         interp_type      = 3;
      }
      else if ( strcmp(argv[arg_index], "-rbm") == 0 )
      {
         arg_index++;
         interp_type      = 1;
      }
      else if ( strcmp(argv[arg_index], "-cri") == 0 )
      {
         arg_index++;
         interp_type      = 2;
      }
      else if ( strcmp(argv[arg_index], "-nf") == 0 )
      {
         arg_index++;
         num_functions = atoi(argv[arg_index++]);
      }
      else if ( strcmp(argv[arg_index], "-funcsfromfile") == 0 )
      {
         arg_index++;
         build_funcs_type      = 1;
         build_funcs_arg_index = arg_index;
      }
      else if ( strcmp(argv[arg_index], "-help") == 0 )
      {
         print_usage = 1;
      }
      else if ( strcmp(argv[arg_index], "-useblock") == 0 )
      {
         arg_index++;
	 use_block_flag = 1;
      }
      else if ( strcmp(argv[arg_index], "-sysL") == 0 )
      {
         arg_index++;
         num_fun = atoi(argv[arg_index++]);
      }
      else if ( strcmp(argv[arg_index], "-print") == 0 )
      {
         arg_index++;
         print_matrix = 1;
      }
      else if ( strcmp(argv[arg_index], "-sys_vcoef") == 0 )
      {
         arg_index++;
         system_vcoef = 1;
      }

      else
      {
         arg_index++;
      }
   }

   /*-----------------------------------------------------------
    * Print usage info
    *-----------------------------------------------------------*/
 
   if (print_usage)
   {
      printf("\n");
      printf("Usage: %s [<options>]\n", argv[0]);
      printf("\n");
      printf("  -fromfile <filename>   : build matrix from file\n");
      printf("\n");
      printf("  -laplacian (default)   : build laplacian 7pt operator\n");
      printf("  -sysL <num functions>  : build SYSTEMS laplacian 7pt operator\n");
      printf("  -stencil <infile>      : build general stencil operator\n");
      printf("  -9pt                   : build laplacian 9pt operator\n");
      printf("  -27pt                  : build laplacian 27pt operator\n");
      printf("  -difconv               : build 7pt diffusion-convection\n");
      printf("  -agg <numlev>          : defines no. levels of aggr. coars. and interp.\n");
      printf("  -a1                    : use A1 coarsening (agg.)\n");
      printf("  -a2                    : use A2 coarsening (agg.)\n");
      printf("  -cljp1                 : use CLJP1 coarsening (agg.)\n");
      printf("  -cljp2                 : use CLJP2 coarsening (agg.)\n");
      printf("  -wLJP                  : use wLJP coarsening (agg.)\n");
      printf("  -wLJP2                 : use wLJP coarsening applied twice (agg.)\n");
      printf("  -ruge1p                : use classical coarsening with only one pass\n");
      printf("  -mp                    : use multipass interpolation (agg.)\n");
      printf("  -jac                   : use multipass interpolation with Jacobi iteration(agg.)\n");
      printf("  -nj <njac>             : defines no. of its of Jacobi interpolation\n");
      printf("  -ruge                  : use classical coarsening \n");
      printf("  -rugeL                 : use classical coarsening \n");
      printf("                           (more efficient version) \n");
      printf("  -cr                    : use coarsening based on compatible relaxation\n");
      printf("  -nrlx <nx>             : no. relaxation step for cr coarsening\n");
      printf("  -rbm                   : use rigid body motion interpolation \n");
      printf("  -rlx <rlxtype>         : rlxtype = 0 Jacobi relaxation \n");
      printf("                                     1 Gauss-Seidel relaxation \n");
      printf("  -w <rlxweight>         : defines relaxation weight for Jacobi\n");
      printf("  -schwarz <numlev>      : defines number of levels of Schwarz used \n");
      printf("  -ns <val>              : Use <val> sweeps on each level\n");
      printf("                           (default C/F down, F/C up, F/C fine\n");
      printf("  -mu <val>              : sets cycle type, 1=V, 2=W, etc\n");
      printf("  -th <threshold>        : defines threshold for coarsenings\n");
      printf("  -Atr <truncfactor>     : defines operator truncation factor\n");
      printf("  -Amx <max_elmts>       : defines max coeffs per row in operator\n");
      printf("  -Pmx <max_elmts>       : defines max coeffs per row in interpolation\n");
      printf("  -Ptr <truncfactor>     : defines interpolation truncation factor\n");
      printf("  -maxlev <ml>           : defines max. no. of levels\n");
      printf("  -n <nx> <ny> <nz>      : problem size per processor\n");
      printf("  -P <Px> <Py> <Pz>      : processor topology\n");
      printf("  -c <cx> <cy> <cz>      : diffusion coefficients\n");
      printf("  -a <ax> <ay> <az>      : convection coefficients\n");
      printf("  -nf <val>              : set number of functions for systems AMG\n");

      printf("\n");
      printf("  -solver <ID>           : solver ID\n");
      printf("      0=AMG, 1=AMG-CG, 2=CG, 3=AMG-GMRES, 4=GMRES\n");
      printf("\n");
      printf("  -print                 : print out the system matrix\n");
      printf("\n");
      exit(1);
   }

   /*-----------------------------------------------------------
    * Check a few things
    *-----------------------------------------------------------*/

   /*-----------------------------------------------------------
    * Print driver parameters
    *-----------------------------------------------------------*/
 
  
      printf("Running with these driver parameters:\n");
      printf("  solver ID    = %d\n", solver_id);
 

   /*-----------------------------------------------------------
    * Set up matrix
    *-----------------------------------------------------------*/

   if ( build_matrix_type == 0 )
   {
      BuildFromFile(argc, argv, build_matrix_arg_index, &A);
   }
   else if ( build_matrix_type == 1 )
   {
      BuildLaplacian(argc, argv, build_matrix_arg_index, &A);
   }
   else if ( build_matrix_type == 2 )
   {
      BuildStencilMatrix(argc, argv, build_matrix_arg_index, &A);
   }
   else if ( build_matrix_type == 3 )
   {
      BuildLaplacian9pt(argc, argv, build_matrix_arg_index, &A);
   }
   else if ( build_matrix_type == 4 )
   {
      BuildLaplacian27pt(argc, argv, build_matrix_arg_index, &A);
   }
   else if ( build_matrix_type == 5 )
   {
      BuildDifConv(argc, argv, build_matrix_arg_index, &A);
   }
   /*-----------------------------------------------------------
    * Set up the RHS and initial guess
    *-----------------------------------------------------------*/

   if (print_matrix)
   {
      hypre_CSRMatrixPrint(A, "driver.out.A");
   }
   

   if ( build_rhs_type == 1 )
   {
      BuildRhsFromFile(argc, argv, build_rhs_arg_index, A, &b);
 
      x = hypre_SeqVectorCreate( hypre_CSRMatrixNumRows(A));
      hypre_SeqVectorInitialize(x);
      hypre_SeqVectorSetConstantValues(x, 0.0);      
   }
   else if ( build_rhs_type == 3 )
   {
      b = hypre_SeqVectorCreate( hypre_CSRMatrixNumRows(A));
      hypre_SeqVectorInitialize(b);
      hypre_SeqVectorSetRandomValues(b, 22775);
      norm = 1.0/sqrt(hypre_SeqVectorInnerProd(b,b));
      ierr = hypre_SeqVectorScale(norm, b);      
 
      x = hypre_SeqVectorCreate( hypre_CSRMatrixNumRows(A));
      hypre_SeqVectorInitialize(x);
      hypre_SeqVectorSetConstantValues(x, 0.0);      
   }
   else if ( build_rhs_type == 4 )
   {
      x = hypre_SeqVectorCreate( hypre_CSRMatrixNumRows(A));
      hypre_SeqVectorInitialize(x);
      hypre_SeqVectorSetConstantValues(x, 1.0);      
 
      b = hypre_SeqVectorCreate( hypre_CSRMatrixNumRows(A));
      hypre_SeqVectorInitialize(b);
      hypre_CSRMatrixMatvec(1.0,A,x,0.0,b);
 
      hypre_SeqVectorSetConstantValues(x, 0.0);      
   }
   else if (build_rhs_type == 5 )
   {
      b = hypre_SeqVectorCreate(hypre_CSRMatrixNumRows(A));
      hypre_SeqVectorInitialize(b);
      hypre_SeqVectorSetConstantValues(b,0.0);

      x = hypre_SeqVectorCreate(hypre_CSRMatrixNumRows(A));
      hypre_SeqVectorInitialize(x);
      hypre_SeqVectorSetConstantValues(x, 1.0);
   }
   else 
   {
      b = hypre_SeqVectorCreate(hypre_CSRMatrixNumRows(A));
      hypre_SeqVectorInitialize(b);
      hypre_SeqVectorSetConstantValues(b,1.0);

      x = hypre_SeqVectorCreate(hypre_CSRMatrixNumRows(A));
      hypre_SeqVectorInitialize(x);
      hypre_SeqVectorSetConstantValues(x, 0.0);
   }
   if ( build_funcs_type == 1 )
   {
      BuildFuncsFromFile(argc, argv, build_funcs_arg_index, &dof_func);    
   }
   else /* if (num_functions > 1) */ 
   {
      printf("\n Number of unknown functions = %d\n\n",num_functions);
      dof_func = hypre_CTAlloc(int,hypre_CSRMatrixNumRows(A));
 
      for (j = 0; j < hypre_CSRMatrixNumRows(A)/num_functions; j++)
      {
         for (k = 0; k < num_functions; k++) dof_func[j*num_functions+k] = k;
      }
   }

   /*-----------------------------------------------------------
    * Solve the system using AMG
    *-----------------------------------------------------------*/

      strong_threshold = 0.25;
      cycle_type       = 1;
      num_sweep = 1;

      num_grid_sweeps = hypre_CTAlloc(int,4);
      grid_relax_type = hypre_CTAlloc(int,4);
      grid_relax_points = hypre_CTAlloc(int *,4);
      relax_weight = hypre_CTAlloc(double,max_levels);
      schwarz_option = hypre_CTAlloc(int,max_levels);
      for (i=0; i < max_levels; i++)
      {
	 relax_weight[i] = 1.0;
	 schwarz_option[i] = -1;
      }

      arg_index = 0;
      while (arg_index < argc)
      {
         if ( strcmp(argv[arg_index], "-w") == 0 )
         {
            arg_index++;
            relax_weight[0] = atof(argv[arg_index++]);
	    for (i=1; i < max_levels; i++)
		relax_weight[i] = relax_weight[0];
         }
         if ( strcmp(argv[arg_index], "-schwarz") == 0 )
         {
            arg_index++;
            schwarz_lev = atoi(argv[arg_index++]);
	    for (i=0; i < schwarz_lev; i++)
		schwarz_option[i] = 1;
         }
         else if ( strcmp(argv[arg_index], "-th") == 0 )
         {
            arg_index++;
            strong_threshold  = atof(argv[arg_index++]);
         }
         else if ( strcmp(argv[arg_index], "-mode") == 0 )
         {
            arg_index++;
            mode  = atof(argv[arg_index++]);
         }
         else if ( strcmp(argv[arg_index], "-Atr") == 0 )
         {
            arg_index++;
            A_trunc_factor  = atof(argv[arg_index++]);
         }
         else if ( strcmp(argv[arg_index], "-Amx") == 0 )
         {
            arg_index++;
            A_max_elmts  = atoi(argv[arg_index++]);
         }
         else if ( strcmp(argv[arg_index], "-Ptr") == 0 )
         {
            arg_index++;
            P_trunc_factor  = atof(argv[arg_index++]);
         }
         else if ( strcmp(argv[arg_index], "-Pmx") == 0 )
         {
            arg_index++;
            P_max_elmts  = atoi(argv[arg_index++]);
         }
         else if ( strcmp(argv[arg_index], "-iout") == 0 )
         {
            arg_index++;
            ioutdat  = atoi(argv[arg_index++]);
         }
         else if ( strcmp(argv[arg_index], "-pl") == 0 )
         {
            arg_index++;
            print_level = atoi(argv[arg_index++]);
         }
         else if (strcmp(argv[arg_index], "-mu") == 0 )
         {
            arg_index++;
            cycle_type = atoi(argv[arg_index++]);
         }
         else if (strcmp(argv[arg_index], "-ns") == 0 )
         {
            arg_index++;
            num_sweep = atoi(argv[arg_index++]);
         }
         else
         {
            arg_index++;
         }
      }

   if (solver_id == 0 || solver_id == 1 || solver_id == 3)
   {
/*----------------------------------------------------------------
 *
 * Option to have numerous relaxation sweeps per level.  In this case
 * we use C/F relaxation going down, F/C going up, for all sweeps
 *
 *----------------------------------------------------------------*/

      /* fine grid */
     num_grid_sweeps[0] = 2*num_sweep;
      grid_relax_type[0] = relax_default; 
      grid_relax_points[0] = hypre_CTAlloc(int, 2*num_sweep); 
      for (i=0; i<2*num_sweep; i+=2)
      {
	grid_relax_points[0][i] = 1;
	  grid_relax_points[0][i+1] = -1;
      }

      /* down cycle */
      num_grid_sweeps[1] = 2*num_sweep;
      grid_relax_type[1] = relax_default; 
      grid_relax_points[1] = hypre_CTAlloc(int, 2*num_sweep); 
      for (i=0; i<2*num_sweep; i+=2)
      {
	grid_relax_points[1][i] = 1;
	grid_relax_points[1][i+1] = -1;
      }

      /* up cycle */
      num_grid_sweeps[2] = 2*num_sweep;
      grid_relax_type[2] = relax_default; 
      grid_relax_points[2] = hypre_CTAlloc(int, 2*num_sweep); 
      for (i=0; i<2*num_sweep; i+=2)
      {
	grid_relax_points[2][i] = -1;
	grid_relax_points[2][i+1] = 1;
      }


      /* coarsest grid */
      num_grid_sweeps[3] = 1;
      grid_relax_type[3] = 9;
      grid_relax_points[3] = hypre_CTAlloc(int, 20);
      for (i=0; i < 20; i++) grid_relax_points[3][i] = 0;
   }

   if (solver_id == 0)
   {

      /* time_index = hypre_InitializeTiming("Setup");
      hypre_BeginTiming(time_index); */

      amg_solver = HYPRE_AMGInitialize();
      HYPRE_AMGSetMaxIter(amg_solver, 20);
      HYPRE_AMGSetCoarsenType(amg_solver, coarsen_type);
      HYPRE_AMGSetAggCoarsenType(amg_solver, agg_coarsen_type);
      HYPRE_AMGSetAggLevels(amg_solver, agg_levels);
      HYPRE_AMGSetStrongThreshold(amg_solver, strong_threshold);
      HYPRE_AMGSetMode(amg_solver, mode);
      HYPRE_AMGSetATruncFactor(amg_solver, A_trunc_factor);
      HYPRE_AMGSetAMaxElmts(amg_solver, A_max_elmts);
      HYPRE_AMGSetPTruncFactor(amg_solver, P_trunc_factor);
      HYPRE_AMGSetPMaxElmts(amg_solver, P_max_elmts);
      HYPRE_AMGSetNumRelaxSteps(amg_solver, num_relax_steps);
      HYPRE_AMGSetIOutDat(amg_solver, print_level);
      HYPRE_AMGSetCycleType(amg_solver, cycle_type);
      HYPRE_AMGSetNumGridSweeps(amg_solver, num_grid_sweeps);
      HYPRE_AMGSetGridRelaxType(amg_solver, grid_relax_type);
      HYPRE_AMGSetGridRelaxPoints(amg_solver, grid_relax_points);
      HYPRE_AMGSetRelaxWeight(amg_solver, relax_weight);
      HYPRE_AMGSetSchwarzOption(amg_solver, schwarz_option);
      HYPRE_AMGSetMaxLevels(amg_solver, max_levels);
      HYPRE_AMGSetInterpType(amg_solver, interp_type);
      HYPRE_AMGSetAggInterpType(amg_solver, agg_interp_type);
      HYPRE_AMGSetNumJacs(amg_solver, num_jacs);
      HYPRE_AMGSetNumFunctions(amg_solver, num_functions);
      HYPRE_AMGSetDofFunc(amg_solver, dof_func);
      HYPRE_AMGSetUseBlockFlag(amg_solver, use_block_flag);

      HYPRE_AMGSetup(amg_solver, (HYPRE_CSRMatrix) A, (HYPRE_Vector) b, 
			(HYPRE_Vector) x);
      /* hypre_EndTiming(time_index);
      hypre_PrintTiming("Setup phase times", MPI_COMM_WORLD);
      hypre_FinalizeTiming(time_index);
      hypre_ClearTiming(); 
 
      time_index = hypre_InitializeTiming("Solve");
      hypre_BeginTiming(time_index); */

      HYPRE_AMGSolve(amg_solver, (HYPRE_CSRMatrix) A, (HYPRE_Vector) b, 
			(HYPRE_Vector) x);

      /* hypre_EndTiming(time_index);
      hypre_PrintTiming("Solve phase times", MPI_COMM_WORLD);
      hypre_FinalizeTiming(time_index);
      hypre_ClearTiming(); */
 
      HYPRE_AMGFinalize(amg_solver);
   }

   /*-----------------------------------------------------
    * Solve the system using PCG
    *-----------------------------------------------------*/

   if (solver_id == 1 || solver_id == 2)
   {
      /* time_index = hypre_InitializeTiming("PCG Setup");
      hypre_BeginTiming(time_index); */

      HYPRE_CSRPCGCreate( &pcg_solver);
      HYPRE_PCGSetMaxIter(pcg_solver, 500);
      HYPRE_PCGSetTol(pcg_solver, tol);
      HYPRE_PCGSetTwoNorm(pcg_solver, 1);
      HYPRE_PCGSetRelChange(pcg_solver, 0);
      HYPRE_PCGSetPrintLevel(pcg_solver, print_level);

      if (solver_id == 1)
      {
	 /* use AMG as preconditioner */
	 printf ("Solver: AMG-PCG\n");
         pcg_precond = HYPRE_AMGInitialize();
         HYPRE_AMGSetCoarsenType(pcg_precond, coarsen_type);
         HYPRE_AMGSetAggCoarsenType(pcg_precond, agg_coarsen_type);
         HYPRE_AMGSetAggLevels(pcg_precond, agg_levels);
         HYPRE_AMGSetStrongThreshold(pcg_precond, strong_threshold);
         HYPRE_AMGSetMode(pcg_precond, mode);
         HYPRE_AMGSetATruncFactor(pcg_precond, A_trunc_factor);
         HYPRE_AMGSetAMaxElmts(pcg_precond, A_max_elmts);
         HYPRE_AMGSetPTruncFactor(pcg_precond, P_trunc_factor);
         HYPRE_AMGSetPMaxElmts(pcg_precond, P_max_elmts);
         HYPRE_AMGSetNumRelaxSteps(pcg_precond, num_relax_steps);
         HYPRE_AMGSetIOutDat(pcg_precond, ioutdat);
         HYPRE_AMGSetMaxIter(pcg_precond, 1);
         HYPRE_AMGSetCycleType(pcg_precond, cycle_type);
         HYPRE_AMGSetNumGridSweeps(pcg_precond, num_grid_sweeps);
         HYPRE_AMGSetGridRelaxType(pcg_precond, grid_relax_type);
         HYPRE_AMGSetGridRelaxPoints(pcg_precond, grid_relax_points);
         HYPRE_AMGSetRelaxWeight(pcg_precond, relax_weight);
         HYPRE_AMGSetSchwarzOption(pcg_precond, schwarz_option);
         HYPRE_AMGSetMaxLevels(pcg_precond, max_levels);
         HYPRE_AMGSetInterpType(pcg_precond, interp_type);
         HYPRE_AMGSetAggInterpType(pcg_precond, agg_interp_type);
         HYPRE_AMGSetNumJacs(pcg_precond, num_jacs);
         HYPRE_AMGSetNumFunctions(pcg_precond, num_functions);
         HYPRE_AMGSetDofFunc(pcg_precond, dof_func);
	 HYPRE_AMGSetUseBlockFlag(pcg_precond, use_block_flag);
         HYPRE_PCGSetPrecond( pcg_solver, (HYPRE_PtrToSolverFcn) HYPRE_AMGSolve,
                              (HYPRE_PtrToSolverFcn) HYPRE_AMGSetup, 
                              pcg_precond);
      }
      else if (solver_id == 2)
      {
	 /* use diagonal scaling as preconditioner */
	 printf ("Solver: DS-PCG\n");
	 pcg_precond = NULL;
	 HYPRE_PCGSetPrecond( pcg_solver, (HYPRE_PtrToSolverFcn) HYPRE_CSRDiagScale,
                              (HYPRE_PtrToSolverFcn) HYPRE_CSRDiagScaleSetup,
                              pcg_precond);
      }

      HYPRE_PCGSetup( pcg_solver, (HYPRE_Matrix) A, (HYPRE_Vector) b, 
                      (HYPRE_Vector) x);
      /* hypre_EndTiming(time_index);
      hypre_PrintTiming("Setup phase times", MPI_COMM_WORLD);
      hypre_FinalizeTiming(time_index);
      hypre_ClearTiming();
 
      time_index = hypre_InitializeTiming("PCG Solve");
      hypre_BeginTiming(time_index); */

      HYPRE_PCGSolve( pcg_solver, (HYPRE_Matrix) A, (HYPRE_Vector) b, 
			(HYPRE_Vector) x);

      /* hypre_EndTiming(time_index);
      hypre_PrintTiming("Solve phase times", MPI_COMM_WORLD);
      hypre_FinalizeTiming(time_index);
      hypre_ClearTiming(); */

      HYPRE_PCGGetNumIterations(pcg_solver, &num_iterations);
      HYPRE_PCGGetFinalRelativeResidualNorm(pcg_solver, &final_res_norm);

      HYPRE_CSRPCGDestroy(pcg_solver);
 
      if (solver_id == 1) HYPRE_AMGFinalize(pcg_precond);
   /*-----------------------------------------------------------
    * Print the solution and other info
    *-----------------------------------------------------------*/

         printf("\n");
         printf("Iterations = %d\n", num_iterations);
         printf("Final Relative Residual Norm = %e\n", final_res_norm);
         printf("\n");
   }

   /*-----------------------------------------------------
    * Solve the system using GMRES
    *-----------------------------------------------------*/

   if (solver_id == 3 || solver_id == 4)
   {
      /* time_index = hypre_InitializeTiming("GMRES Setup");
      hypre_BeginTiming(time_index); */

      HYPRE_CSRGMRESCreate( &pcg_solver);
      HYPRE_GMRESSetMaxIter(pcg_solver, 500);
      HYPRE_GMRESSetTol(pcg_solver, tol);
      HYPRE_GMRESSetKDim(pcg_solver, k_dim);
      HYPRE_GMRESSetPrintLevel(pcg_solver, print_level);

      if (solver_id == 3)
      {
	 /* use AMG as preconditioner */
	 printf ("Solver: AMG-GMRES\n");
         pcg_precond = HYPRE_AMGInitialize();
         HYPRE_AMGSetCoarsenType(pcg_precond, coarsen_type);
         HYPRE_AMGSetAggCoarsenType(pcg_precond, agg_coarsen_type);
         HYPRE_AMGSetAggLevels(pcg_precond, agg_levels);
         HYPRE_AMGSetStrongThreshold(pcg_precond, strong_threshold);
         HYPRE_AMGSetMode(pcg_precond, mode);
         HYPRE_AMGSetATruncFactor(pcg_precond, A_trunc_factor);
         HYPRE_AMGSetAMaxElmts(pcg_precond, A_max_elmts);
         HYPRE_AMGSetPTruncFactor(pcg_precond, P_trunc_factor);
         HYPRE_AMGSetPMaxElmts(pcg_precond, P_max_elmts);
         HYPRE_AMGSetNumRelaxSteps(pcg_precond, num_relax_steps);
         HYPRE_AMGSetIOutDat(pcg_precond, ioutdat);
         HYPRE_AMGSetMaxIter(pcg_precond, 1);
         HYPRE_AMGSetCycleType(pcg_precond, cycle_type);
         HYPRE_AMGSetNumGridSweeps(pcg_precond, num_grid_sweeps);
         HYPRE_AMGSetGridRelaxType(pcg_precond, grid_relax_type);
         HYPRE_AMGSetGridRelaxPoints(pcg_precond, grid_relax_points);
         HYPRE_AMGSetRelaxWeight(pcg_precond, relax_weight);
         HYPRE_AMGSetSchwarzOption(pcg_precond, schwarz_option);
         HYPRE_AMGSetMaxLevels(pcg_precond, max_levels);
         HYPRE_AMGSetInterpType(pcg_precond, interp_type);
         HYPRE_AMGSetAggInterpType(pcg_precond, agg_interp_type);
         HYPRE_AMGSetNumJacs(pcg_precond, num_jacs);
         HYPRE_AMGSetNumFunctions(pcg_precond, num_functions);
         HYPRE_AMGSetDofFunc(pcg_precond, dof_func);
         HYPRE_GMRESSetPrecond( pcg_solver, (HYPRE_PtrToSolverFcn) HYPRE_AMGSolve,
                                (HYPRE_PtrToSolverFcn) HYPRE_AMGSetup, 
				pcg_precond);
      }
      else if (solver_id == 2)
      {
	 /* use diagonal scaling as preconditioner */
	 printf ("Solver: DS-GMRES\n");
	 pcg_precond = NULL;
	 HYPRE_GMRESSetPrecond( pcg_solver, (HYPRE_PtrToSolverFcn) HYPRE_CSRDiagScale,
				(HYPRE_PtrToSolverFcn) HYPRE_CSRDiagScaleSetup,
                                pcg_precond);
      }

      HYPRE_GMRESSetup( pcg_solver, (HYPRE_Matrix) A, (HYPRE_Vector) b, 
			(HYPRE_Vector) x);
      /* hypre_EndTiming(time_index);
      hypre_PrintTiming("Setup phase times", MPI_COMM_WORLD);
      hypre_FinalizeTiming(time_index);
      hypre_ClearTiming();
 
      time_index = hypre_InitializeTiming("GMRES Solve");
      hypre_BeginTiming(time_index); */

      HYPRE_GMRESSolve( pcg_solver, (HYPRE_Matrix) A, (HYPRE_Vector) b, 
			(HYPRE_Vector) x);

      /* hypre_EndTiming(time_index);
      hypre_PrintTiming("Solve phase times", MPI_COMM_WORLD);
      hypre_FinalizeTiming(time_index);
      hypre_ClearTiming(); */

      HYPRE_GMRESGetNumIterations(pcg_solver, &num_iterations);
      HYPRE_GMRESGetFinalRelativeResidualNorm(pcg_solver, &final_res_norm);

      HYPRE_CSRGMRESDestroy(pcg_solver);
 
      if (solver_id == 1) HYPRE_AMGFinalize(pcg_precond);
   /*-----------------------------------------------------------
    * Print the solution and other info
    *-----------------------------------------------------------*/

         printf("\n");
         printf("Iterations = %d\n", num_iterations);
         printf("Final Relative Residual Norm = %e\n", final_res_norm);
         printf("\n");
   }

#if 1
   hypre_SeqVectorPrint(x, "driver.out.x");
#endif

   /*-----------------------------------------------------------
    * Finalize things
    *-----------------------------------------------------------*/

#if 0
   hypre_DestroyParCSRMatrix(A);
   hypre_DestroyParVector(b);
   hypre_DestroyParVector(x);
#endif
   hypre_CSRMatrixDestroy(A);
   hypre_SeqVectorDestroy(b);
   hypre_SeqVectorDestroy(x);

#if 0
   hypre_TFree(global_part);
#endif
/*
   hypre_FinalizeMemoryDebug();
*/
   /* Finalize MPI */
   /* MPI_Finalize(); */
#if 0
#endif

   return (ierr);
}

/*----------------------------------------------------------------------
 * Build matrix from file.
 * Parameters given in command line.
 *----------------------------------------------------------------------*/

int
BuildFromFile( int               argc,
               char             *argv[],
               int               arg_index,
               hypre_CSRMatrix **A_ptr     )
{
   char               *filename;

#if 0
   hypre_ParCSRMatrix *A;
   int 		      *global_part;
#endif
   hypre_CSRMatrix    *A;

   int                 myid;

   /*-----------------------------------------------------------
    * Initialize some stuff
    *-----------------------------------------------------------*/

#if 0 
   MPI_Comm_rank(MPI_COMM_WORLD, &myid );
#endif

   myid = 0;

   /*-----------------------------------------------------------
    * Parse command line
    *-----------------------------------------------------------*/

   if (arg_index < argc)
   {
      filename = argv[arg_index];
   }
   else
   {
      printf("Error: No filename specified \n");
      exit(1);
   }

   /*-----------------------------------------------------------
    * Print driver parameters
    *-----------------------------------------------------------*/
 
   if (myid == 0)
   {
      printf("  Operator FromFile: %s\n", filename);
   }

   /*-----------------------------------------------------------
    * Generate the matrix 
    *-----------------------------------------------------------*/
 
   A = hypre_CSRMatrixRead(filename);
   hypre_CSRMatrixReorder(A);
   
   *A_ptr = A;
#if 0
   *global_part_ptr = global_part;
#endif

   return (0);
}

/*----------------------------------------------------------------------
 * Build standard 7-point laplacian in 3D with grid and anisotropy.
 * Parameters given in command line.
 *----------------------------------------------------------------------*/

int
BuildLaplacian( int               argc,
                char             *argv[],
                int               arg_index,
                hypre_CSRMatrix **A_ptr     )
{
   int                 nx, ny, nz;
   int                 P, Q, R;
   double              cx, cy, cz;
   int                 num_fun = 1;
   double             *values;
   double             *mtrx;
  


#if 0
   hypre_ParCSRMatrix *A;
   int 		      *global_part;
   int                 p, q, r;
#endif
   hypre_CSRMatrix    *A;

   int                 num_procs, myid;

   int                 system_vcoef = 0;
   


   /*-----------------------------------------------------------
    * Initialize some stuff
    *-----------------------------------------------------------*/

#if 0 
   MPI_Comm_size(MPI_COMM_WORLD, &num_procs );
   MPI_Comm_rank(MPI_COMM_WORLD, &myid );
#endif

   num_procs = 1;
   myid = 0;

   /*-----------------------------------------------------------
    * Set defaults
    *-----------------------------------------------------------*/
 
   nx = 10;
   ny = 10;
   nz = 10;

   P  = 1;
   Q  = num_procs;
   R  = 1;

   cx = 1.0;
   cy = 1.0;
   cz = 1.0;

   /*-----------------------------------------------------------
    * Parse command line
    *-----------------------------------------------------------*/
   arg_index = 0; 
   while (arg_index < argc)
   {
      if ( strcmp(argv[arg_index], "-n") == 0 )
      {
         arg_index++;
         nx = atoi(argv[arg_index++]);
         ny = atoi(argv[arg_index++]);
         nz = atoi(argv[arg_index++]);
      }
      else if ( strcmp(argv[arg_index], "-P") == 0 )
      {
         arg_index++;
         P  = atoi(argv[arg_index++]);
         Q  = atoi(argv[arg_index++]);
         R  = atoi(argv[arg_index++]);
      }
      else if ( strcmp(argv[arg_index], "-c") == 0 )
      {
         arg_index++;
         cx = atof(argv[arg_index++]);
         cy = atof(argv[arg_index++]);
         cz = atof(argv[arg_index++]);
      }
      else if ( strcmp(argv[arg_index], "-sysL") == 0 )
      {
         arg_index++;
         num_fun = atoi(argv[arg_index++]);
      }
      else if ( strcmp(argv[arg_index], "-sys_vcoef") == 0 )
      {
         arg_index++;
         system_vcoef = 1;
      }
      else
      {
         arg_index++;
      }
   }

   /*-----------------------------------------------------------
    * Check a few things
    *-----------------------------------------------------------*/
/*
   if ((P*Q*R) != num_procs)
   {
      printf("Error: Invalid number of processors or processor topology \n");
      exit(1);
   }
*/
   /*-----------------------------------------------------------
    * Print driver parameters
    *-----------------------------------------------------------*/
 
   if (myid == 0)
   {
      printf("  Laplacian:\n");
      printf("  Laplacian:   num_fun = %d\n", num_fun);
      printf("    (nx, ny, nz) = (%d, %d, %d)\n", nx, ny, nz);
      printf("    (Px, Py, Pz) = (%d, %d, %d)\n", P,  Q,  R);
      printf("    (cx, cy, cz) = (%f, %f, %f)\n", cx, cy, cz);
   }

   /*-----------------------------------------------------------
    * Generate the matrix 
    *-----------------------------------------------------------*/
 
   values = hypre_CTAlloc(double, 4);

   values[1] = -cx;
   values[2] = -cy;
   values[3] = -cz;

   values[0] = 0.0;
   if (nx > 1)
   {
      values[0] += 2.0*cx;
   }
   if (ny > 1)
   {
      values[0] += 2.0*cy;
   }
   if (nz > 1)
   {
      values[0] += 2.0*cz;
   }

   /*-----------------------------------------------------------
    * Set up the grid structure
    *-----------------------------------------------------------*/

   /* compute p,q,r from P,Q,R and myid */

#if 0   
   p = myid % P;
   q = (( myid - p)/P) % Q;
   r = ( myid - p - P*q)/( P*Q );
   A = hypre_GenerateLaplacian(MPI_COMM_WORLD,
                               nx, ny, nz, P, Q, R, p, q, r,
                               values, &global_part);
#endif

   if (num_fun ==1)
   {
      A = hypre_GenerateLaplacian(nx, ny, nz, P, Q, R, values);
   }
   else
   {

      mtrx = hypre_CTAlloc(double, num_fun*num_fun);
      
      if (num_fun == 2)
      {
         mtrx[0] = 2;
         mtrx[1] = 1;
         mtrx[2] = 1;
         mtrx[3] = 2;

#if 0         
         mtrx[0] = .01;
         mtrx[1] = 200; 
         mtrx[2] = 200;
         mtrx[3] = .01; 
#endif
    

      }
      else if (num_fun == 3)
      {
         mtrx[0] = 1.01;
         mtrx[1] = 1;
         mtrx[2] = 0.0;
         mtrx[3] = 1;
         mtrx[4] = 2;
         mtrx[5] = 1;
         mtrx[6] = 0.0;
         mtrx[7] = 1;
         mtrx[8] = 1.01;

#if 0
         mtrx[0] = 3.0;
         mtrx[1] = 1;
         mtrx[2] = 0.0;
         mtrx[3] = 1;
         mtrx[4] = 4;
         mtrx[5] = 2;
         mtrx[6] = 0.0;
         mtrx[7] = 2;
         mtrx[8] = .25;
#endif

      }

      if (!system_vcoef)
      {
         A = hypre_GenerateSysLaplacian(nx, ny, nz, P, Q, R, num_fun, mtrx, values);
      }
      else
      {
         
         double *mtrx_values;

         mtrx_values = hypre_CTAlloc(double, num_fun*num_fun*4);


         if (num_fun == 2)
         {


            mtrx[0] = 2;
            SetSysVcoefValues(num_fun, nx, ny, nz, 1.0, 1.0, 1.0, 0, mtrx_values);
            
            mtrx[1] = 1;
            SetSysVcoefValues(num_fun, nx, ny, nz, 1.0, 2.0, 1.0, 1, mtrx_values);
            
            mtrx[2] = 1;
            SetSysVcoefValues(num_fun, nx, ny, nz, 2.0, 1.0, 0.0, 2, mtrx_values);
            
            mtrx[3] = 2;
            SetSysVcoefValues(num_fun, nx, ny, nz, 1.0, 3.0, 1.0, 3, mtrx_values);

                     
         }
         else if (num_fun == 3)
         {

            mtrx[0] = 1;
            SetSysVcoefValues(num_fun, nx, ny, nz, 1, .01, 1, 0, mtrx_values);

            mtrx[1] = 1;
            SetSysVcoefValues(num_fun, nx, ny, nz, 1, 1, 1, 1, mtrx_values);

            mtrx[2] = 0.0;
            SetSysVcoefValues(num_fun, nx, ny, nz, 1, 1, 1, 2, mtrx_values);

            mtrx[3] = 1;
            SetSysVcoefValues(num_fun, nx, ny, nz, 1, 1, 1, 3, mtrx_values);

            mtrx[4] = 1;
            SetSysVcoefValues(num_fun, nx, ny, nz, 2, .02, 1, 4, mtrx_values);

            mtrx[5] = 2;
            SetSysVcoefValues(num_fun, nx, ny, nz, 1, 1, 1, 5, mtrx_values);

            mtrx[6] = 0.0;
            SetSysVcoefValues(num_fun, nx, ny, nz, 1, 1, 1, 6, mtrx_values);

            mtrx[7] = 2;
            SetSysVcoefValues(num_fun, nx, ny, nz, 1, 1, 1, 7, mtrx_values);

            mtrx[8] = 1;
            SetSysVcoefValues(num_fun, nx, ny, nz, 1.5, .04, 1, 8, mtrx_values);
            



         }

         A = hypre_GenerateSysLaplacianVCoef(nx, ny, nz, P, Q, R, num_fun, mtrx, mtrx_values);
         free(mtrx_values);
         

      }
      

      hypre_TFree(mtrx);
   }

   hypre_TFree(values);

   *A_ptr = A;
#if 0
   *global_part_ptr = global_part;
#endif

   return (0);
}

/*----------------------------------------------------------------------
 * Build matrix with general stencil.
 *----------------------------------------------------------------------*/

int
BuildStencilMatrix( int               argc,
                    char             *argv[],
                    int               arg_index,
                    hypre_CSRMatrix **A_ptr )
{
   int                 nx, ny, nz;
   char               *filename;
   hypre_CSRMatrix    *A;

   /*-----------------------------------------------------------
    * Set defaults
    *-----------------------------------------------------------*/
 
   nx = 10;
   ny = 10;
   nz = 10;

   /*-----------------------------------------------------------
    * Parse command line
    *-----------------------------------------------------------*/

   filename = argv[arg_index];

   arg_index = 0; 
   while (arg_index < argc)
   {
      if ( strcmp(argv[arg_index], "-n") == 0 )
      {
         arg_index++;
         nx = atoi(argv[arg_index++]);
         ny = atoi(argv[arg_index++]);
         nz = atoi(argv[arg_index++]);
      }
      else
      {
         arg_index++;
      }
   }

   /*-----------------------------------------------------------
    * Print driver parameters
    *-----------------------------------------------------------*/
 
   printf("  Stencil Matrix:\n");
   printf("    (nx, ny, nz) = (%d, %d, %d)\n", nx, ny, nz);

   /*-----------------------------------------------------------
    * Generate the matrix 
    *-----------------------------------------------------------*/
 
   A = hypre_GenerateStencilMatrix(nx, ny, nz, filename);

   *A_ptr = A;

   return (0);
}

/*----------------------------------------------------------------------
 * Build standard 9-point laplacian in 2D 
 * Parameters given in command line.
 *----------------------------------------------------------------------*/

int
BuildLaplacian9pt( int               argc,
                   char             *argv[],
                   int               arg_index,
                   hypre_CSRMatrix **A_ptr     )
{
   int                 nx, ny;
   int                 P, Q;

#if 0
   hypre_ParCSRMatrix *A;
   int 		      *global_part;
   int                 p, q;
#endif
   hypre_CSRMatrix    *A;

   int                 num_procs, myid;
   double             *values;

   /*-----------------------------------------------------------
    * Initialize some stuff
    *-----------------------------------------------------------*/

#if 0 
   MPI_Comm_size(MPI_COMM_WORLD, &num_procs );
   MPI_Comm_rank(MPI_COMM_WORLD, &myid );
#endif

   num_procs = 1;
   myid = 0;

   /*-----------------------------------------------------------
    * Set defaults
    *-----------------------------------------------------------*/
 
   nx = 10;
   ny = 10;

   P  = 1;
   Q  = num_procs;

   /*-----------------------------------------------------------
    * Parse command line
    *-----------------------------------------------------------*/
   arg_index = 0; 
   while (arg_index < argc)
   {
      if ( strcmp(argv[arg_index], "-n") == 0 )
      {
         arg_index++;
         nx = atoi(argv[arg_index++]);
         ny = atoi(argv[arg_index++]);
      }
      else if ( strcmp(argv[arg_index], "-P") == 0 )
      {
         arg_index++;
         P  = atoi(argv[arg_index++]);
         Q  = atoi(argv[arg_index++]);
      }
      else
      {
         arg_index++;
      }
   }

   /*-----------------------------------------------------------
    * Check a few things
    *-----------------------------------------------------------*/
/*
   if ((P*Q) != num_procs)
   {
      printf("Error: Invalid number of processors or processor topology \n");
      exit(1);
   }
*/
   /*-----------------------------------------------------------
    * Print driver parameters
    *-----------------------------------------------------------*/
 
   if (myid == 0)
   {
      printf("  Laplacian:\n");
      printf("    (nx, ny) = (%d, %d)\n", nx, ny);
      printf("    (Px, Py) = (%d, %d)\n", P,  Q);
   }

   /*-----------------------------------------------------------
    * Generate the matrix 
    *-----------------------------------------------------------*/
 
   values = hypre_CTAlloc(double, 2);

   values[1] = -1.0;

   values[0] = 0.0;
   if (nx > 1)
   {
      values[0] += 2.0;
   }
   if (ny > 1)
   {
      values[0] += 2.0;
   }
   if (nx > 1 && ny > 1)
   {
      values[0] += 4.0;
   }

   /*-----------------------------------------------------------
    * Set up the grid structure
    *-----------------------------------------------------------*/

   /* compute p,q from P,Q and myid */

#if 0   
   p = myid % P;
   q = ( myid - p)/P;
   A = hypre_GenerateLaplacian9pt(MPI_COMM_WORLD,
                               nx, ny, P, Q, p, q,
                               values, &global_part);
#endif
   A = hypre_GenerateLaplacian9pt(nx, ny, P, Q, values);

   hypre_TFree(values);

   *A_ptr = A;
#if 0
   *global_part_ptr = global_part;
#endif

   return (0);
}

/*----------------------------------------------------------------------
 * Build standard 27-point laplacian in 3D 
 * Parameters given in command line.
 *----------------------------------------------------------------------*/

int
BuildLaplacian27pt(int               argc,
                   char             *argv[],
                   int               arg_index,
                   hypre_CSRMatrix **A_ptr     )
{
   int                 nx, ny, nz;
   int                 P, Q, R;

#if 0
   hypre_ParCSRMatrix *A;
   int 		      *global_part;
   int                 p, q, r;
#endif
   hypre_CSRMatrix    *A;

   int                 num_procs, myid;
   double             *values;

   /*-----------------------------------------------------------
    * Initialize some stuff
    *-----------------------------------------------------------*/

   /*-----------------------------------------------------------
    * Set up the grid structure
    *-----------------------------------------------------------*/

   /* compute p,q,r from P,Q, R and myid */
#if 0 
   MPI_Comm_size(MPI_COMM_WORLD, &num_procs );
   MPI_Comm_rank(MPI_COMM_WORLD, &myid );
   p = myid % P;
   q = ( myid - p)/P;

#endif

   num_procs = 1;
   myid = 0;

   /*-----------------------------------------------------------
    * Set defaults
    *-----------------------------------------------------------*/
 
   nx = 10;
   ny = 10;
   nz = 10;

   P  = 1;
   Q  = num_procs;
   R  = 1;

   /*-----------------------------------------------------------
    * Parse command line
    *-----------------------------------------------------------*/
   arg_index = 0; 
   while (arg_index < argc)
   {
      if ( strcmp(argv[arg_index], "-n") == 0 )
      {
         arg_index++;
         nx = atoi(argv[arg_index++]);
         ny = atoi(argv[arg_index++]);
         nz = atoi(argv[arg_index++]);
      }
      else if ( strcmp(argv[arg_index], "-P") == 0 )
      {
         arg_index++;
         P  = atoi(argv[arg_index++]);
         Q  = atoi(argv[arg_index++]);
         R  = atoi(argv[arg_index++]);
      }
      else
      {
         arg_index++;
      }
   }

   /*-----------------------------------------------------------
    * Print driver parameters
    *-----------------------------------------------------------*/
 
   if (myid == 0)
   {
      printf("  27pt_Laplacian:\n");
      printf("    (nx, ny) = (%d, %d, %d)\n", nx, ny, nz);
      printf("    (Px, Py) = (%d, %d, %d)\n", P,  Q, R);
   }

   /*-----------------------------------------------------------
    * Generate the matrix 
    *-----------------------------------------------------------*/
 
   values = hypre_CTAlloc(double, 2);

   values[1] = -1.0;

   values[0] = 26.0;
   if (nx == 1 || ny == 1 || nz == 1)
   {
      values[0] = 8.0;
   }
   if (ny*nx == 1 || ny*nz == 1 || nx*ny == 1)
   {
      values[0] = 2.0;
   }

   A = hypre_GenerateLaplacian27pt(nx, ny, nz, P, Q, R, values);

   hypre_TFree(values);

   *A_ptr = A;
#if 0
   *global_part_ptr = global_part;
#endif

   return (0);
}
/*----------------------------------------------------------------------
 * Build standard 7-point convection-diffusion in 3D anisotropy.
 * Parameters given in command line.
 *
 *  Operator:
 *
 *  -cx Dxx - cy Dyy - cz Dzz + ax Dx + ay Dy + az Dz = f
 *
 *----------------------------------------------------------------------*/

int
BuildDifConv(   int               argc,
                char             *argv[],
                int               arg_index,
                hypre_CSRMatrix **A_ptr     )
{
   int                 nx, ny, nz;
   int                 P, Q, R;
   double              cx, cy, cz;
   double              ax, ay, az;
   double              hinx,hiny,hinz;

#if 0
   hypre_ParCSRMatrix *A;
   int 		      *global_part;
   int                 p, q, r;
#endif
   hypre_CSRMatrix    *A;

   int                 num_procs, myid;
   double             *values;

   /*-----------------------------------------------------------
    * Initialize some stuff
    *-----------------------------------------------------------*/

   /*-----------------------------------------------------------
    * Set up the grid structure
    *-----------------------------------------------------------*/

   /* compute p,q,r from P,Q,R and myid */
#if 0 
   MPI_Comm_size(MPI_COMM_WORLD, &num_procs );
   MPI_Comm_rank(MPI_COMM_WORLD, &myid );
   p = myid % P;
   q = (( myid - p)/P) % Q;
   r = ( myid - p - P*q)/( P*Q );
#endif

   num_procs = 1;
   myid = 0;

   /*-----------------------------------------------------------
    * Set defaults
    *-----------------------------------------------------------*/
 
   nx = 10;
   ny = 10;
   nz = 10;

   hinx = 1.0/(nx+1);
   hiny = 1.0/(ny+1);
   hinz = 1.0/(nz+1);

   P  = 1;
   Q  = num_procs;
   R  = 1;

   cx = 1.0;
   cy = 1.0;
   cz = 1.0;

   ax = 1.0;
   ay = 1.0;
   az = 1.0;

   /*-----------------------------------------------------------
    * Parse command line
    *-----------------------------------------------------------*/
   arg_index = 0; 
   while (arg_index < argc)
   {
      if ( strcmp(argv[arg_index], "-n") == 0 )
      {
         arg_index++;
         nx = atoi(argv[arg_index++]);
         ny = atoi(argv[arg_index++]);
         nz = atoi(argv[arg_index++]);
      }
      else if ( strcmp(argv[arg_index], "-P") == 0 )
      {
         arg_index++;
         P  = atoi(argv[arg_index++]);
         Q  = atoi(argv[arg_index++]);
         R  = atoi(argv[arg_index++]);
      }
      else if ( strcmp(argv[arg_index], "-c") == 0 )
      {
         arg_index++;
         cx = atof(argv[arg_index++]);
         cy = atof(argv[arg_index++]);
         cz = atof(argv[arg_index++]);
      }
      else if ( strcmp(argv[arg_index], "-a") == 0 )
      {
         arg_index++;
         ax = atof(argv[arg_index++]);
         ay = atof(argv[arg_index++]);
         az = atof(argv[arg_index++]);
      }
      else
      {
         arg_index++;
      }
   }

   /*-----------------------------------------------------------
    * Check a few things
    *-----------------------------------------------------------*/
/*
   if ((P*Q*R) != num_procs)
   {
      printf("Error: Invalid number of processors or processor topology \n");
      exit(1);
   }
*/
   /*-----------------------------------------------------------
    * Print driver parameters
    *-----------------------------------------------------------*/
 
   if (myid == 0)
   {
      printf("  Convection-Diffusion: \n");
      printf("    -cx Dxx - cy Dyy - cz Dzz + ax Dx + ay Dy + az Dz = f\n");
      printf("    (nx, ny, nz) = (%d, %d, %d)\n", nx, ny, nz);
      printf("    (Px, Py, Pz) = (%d, %d, %d)\n", P,  Q,  R);
      printf("    (cx, cy, cz) = (%f, %f, %f)\n", cx, cy, cz);
      printf("    (ax, ay, az) = (%f, %f, %f)\n", ax, ay, az);
   }


   /*-----------------------------------------------------------
    * Generate the matrix 
    *-----------------------------------------------------------*/
 
   values = hypre_CTAlloc(double, 7);

   values[1] = -cx/(hinx*hinx);
   values[2] = -cy/(hiny*hiny);
   values[3] = -cz/(hinz*hinz);
   values[4] = -cx/(hinx*hinx) + ax/hinx;
   values[5] = -cy/(hiny*hiny) + ay/hiny;
   values[6] = -cz/(hinz*hinz) + az/hinz;

   values[0] = 0.0;
   if (nx > 1)
   {
      values[0] += 2.0*cx/(hinx*hinx) - 1.0*ax/hinx;
   }
   if (ny > 1)
   {
      values[0] += 2.0*cy/(hiny*hiny) - 1.0*ay/hiny;
   }
   if (nz > 1)
   {
      values[0] += 2.0*cz/(hinz*hinz) - 1.0*az/hinz;
   }

   A = hypre_GenerateDifConv(nx, ny, nz, P, Q, R, values);

   hypre_TFree(values);

   *A_ptr = A;
#if 0
   *global_part_ptr = global_part;
#endif

   return (0);
}

/********************************************************************
 *      
 * Build RHS
 *
 *********************************************************************/

int
BuildRhsFromFile( int                  argc,
                  char                *argv[],
                  int                  arg_index,
                  hypre_CSRMatrix  *A,
                  hypre_Vector    **b_ptr     )
{
   char               *filename;
 
   hypre_Vector *b;
 
   /*-----------------------------------------------------------
    * Parse command line
    *-----------------------------------------------------------*/
 
   if (arg_index < argc)
   {
      filename = argv[arg_index];
   }
   else
   {
      printf("Error: No filename specified \n");
      exit(1);
   }
 
   /*-----------------------------------------------------------
    * Print driver parameters
    *-----------------------------------------------------------*/
 
   printf("  Rhs FromFile: %s\n", filename);
 
   /*-----------------------------------------------------------
    * Generate the matrix 
    *-----------------------------------------------------------*/
 
   b = hypre_SeqVectorRead(filename);
 
   *b_ptr = b;
 
   return (0);
}


/********************************************************************
 *      
 * Build dof_func vector
 *
 *********************************************************************/

int
BuildFuncsFromFile( int                  argc,
                    char                *argv[],
                    int                  arg_index,
                    int                 **dof_func_ptr     )
{
   char               *filename;

   FILE    *fp;

   int     *dof_func;
   int      size;
   
   int      j;


 
   /*-----------------------------------------------------------
    * Parse command line
    *-----------------------------------------------------------*/
 
   if (arg_index < argc)
   {
      filename = argv[arg_index];
   }
   else
   {
      printf("Error: No filename specified \n");
      exit(1);
   }
 
   /*-----------------------------------------------------------
    * Print driver parameters
    *-----------------------------------------------------------*/
 
   printf("\n  FuncsFromFile: %s\n", filename);
 
   /*-----------------------------------------------------------
    * Generate the matrix 
    *-----------------------------------------------------------*/

   /*----------------------------------------------------------
    * Read in the data
    *----------------------------------------------------------*/

   fp = fopen(filename, "r");

   fscanf(fp, "%d", &size);
   dof_func = hypre_CTAlloc(int, size);

   for (j = 0; j < size; j++)
   {
      fscanf(fp, "%d", &dof_func[j]);
   }

   fclose(fp);
  *dof_func_ptr = dof_func;
 
   return (0);
}


/**************************************************************************/


int SetSysVcoefValues(int num_fun, int nx, int ny, int nz, double vcx, 
                      double vcy, double vcz, int mtx_entry, double *values)
{


   int sz = num_fun*num_fun;

   values[1*sz + mtx_entry] = -vcx;
   values[2*sz + mtx_entry] = -vcy;
   values[3*sz + mtx_entry] = -vcz;
   values[0*sz + mtx_entry] = 0.0;

   if (nx > 1)
   {
      values[0*sz + mtx_entry] += 2.0*vcx;
   }
   if (ny > 1)
   {
      values[0*sz + mtx_entry] += 2.0*vcy;
   }
   if (nz > 1)
   {
      values[0*sz + mtx_entry] += 2.0*vcz;
   }

   return 0;
   
   
}

   
