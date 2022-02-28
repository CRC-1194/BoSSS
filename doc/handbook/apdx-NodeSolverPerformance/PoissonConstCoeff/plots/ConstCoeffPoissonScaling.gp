set output 'ConstCoeffPoissonScaling.tex'
set terminal cairolatex  pdf   size 14cm,12cm
set multiplot
set size 1,0.3333333333333333
set size 0.98,0.32666666666666666
set origin 0.01,0.6699999999999999
set lmargin 1e01
set rmargin 5e00
set tmargin 0e00
set bmargin 1.5e00
set logscale x 10
set logscale y 10
unset logscale x2
unset logscale y2
set xrange [1e01:1e07]
set yrange [1e-02:1e04]
set autoscale x2
set autoscale y2
unset xlabel
set ylabel "$k = 2$"
unset x2label
unset y2label
unset title 
unset key
set key font ",16" inside top left Left reverse 
set xtics format " " 
set x2tics format " " 
set ytics format "$10^{%L}$" 
set ytics font "sans, 16" 
set y2tics format " " 
set termoption dashed
plot "ConstCoeffPoissonScaling_data_0.csv" title "GMRES w. pTG" with linespoints linecolor  "black" dashtype 1 linewidth 3 pointtype 5 pointsize 0.5, "ConstCoeffPoissonScaling_data_1.csv" title "Kcycle w. add.-Schwarz" with linespoints linecolor  "black" dashtype 1 linewidth 3 pointtype 9 pointsize 0.5, "ConstCoeffPoissonScaling_data_2.csv" title "Pardiso" with linespoints linecolor  "black" dashtype 3 linewidth 3 pointtype 6 pointsize 0.5, "ConstCoeffPoissonScaling_data_3.csv" title "linear" with lines linecolor  "black" dashtype 1 linewidth 1
set size 0.98,0.32666666666666666
set origin 0.01,0.33666666666666667
set lmargin 1e01
set rmargin 5e00
set tmargin 0e00
set bmargin 1.5e00
set logscale x 10
set logscale y 10
unset logscale x2
unset logscale y2
set xrange [1e01:1e07]
set yrange [1e-02:1e04]
set autoscale x2
set autoscale y2
unset xlabel
set ylabel "$k = 3$"
unset x2label
unset y2label
unset title 
set key off
set xtics format " " 
set x2tics format " " 
set ytics format "$10^{%L}$" 
set ytics font "sans, 16" 
set y2tics format " " 
set termoption dashed
plot "ConstCoeffPoissonScaling_data_4.csv" title "Kcycle w. add.-Schwarz" with linespoints linecolor  "black" dashtype 1 linewidth 3 pointtype 9 pointsize 0.5, "ConstCoeffPoissonScaling_data_5.csv" title "GMRES w. pTG" with linespoints linecolor  "black" dashtype 1 linewidth 3 pointtype 5 pointsize 0.5, "ConstCoeffPoissonScaling_data_6.csv" title "Pardiso" with linespoints linecolor  "black" dashtype 3 linewidth 3 pointtype 6 pointsize 0.5, "ConstCoeffPoissonScaling_data_7.csv" title "linear" with lines linecolor  "black" dashtype 1 linewidth 1
set size 0.98,0.32666666666666666
set origin 0.01,0.003333333333333333
set lmargin 1e01
set rmargin 5e00
set tmargin 0e00
set bmargin 1.5e00
set logscale x 10
set logscale y 10
unset logscale x2
unset logscale y2
set xrange [1e01:1e07]
set yrange [1e-02:1e04]
set autoscale x2
set autoscale y2
set xlabel "DOF"
set ylabel "$k = 5$"
unset x2label
unset y2label
unset title 
set key off
set xtics format "$10^{%L}$" 
set xtics offset 0, 0-0.4 font "sans, 18" 
set xtics font "sans, 16" 
set x2tics format " " 
set ytics format "$10^{%L}$" 
set ytics font "sans, 16" 
set y2tics format " " 
set termoption dashed
plot "ConstCoeffPoissonScaling_data_8.csv" title "GMRES w. pTG" with linespoints linecolor  "black" dashtype 1 linewidth 3 pointtype 5 pointsize 0.5, "ConstCoeffPoissonScaling_data_9.csv" title "Kcycle w. add.-Schwarz" with linespoints linecolor  "black" dashtype 1 linewidth 3 pointtype 9 pointsize 0.5, "ConstCoeffPoissonScaling_data_10.csv" title "Pardiso" with linespoints linecolor  "black" dashtype 3 linewidth 3 pointtype 6 pointsize 0.5, "ConstCoeffPoissonScaling_data_11.csv" title "linear" with lines linecolor  "black" dashtype 1 linewidth 1


exit
