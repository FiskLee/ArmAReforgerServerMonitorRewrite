   M S B A M L   `   `   `  ����U  QLiveCharts.Wpf, Version=0.9.7.0, Culture=neutral, PublicKeyToken=0bc1f845d1ebb8dfG4clr-namespace:LiveCharts.Wpf;assembly=LiveCharts.WpfLiveCharts.Wpf  5      & "ArmaReforgerServerMonitor.Frontend3   -ArmaReforgerServerMonitor.Frontend.MainWindow   5      W SPresentationCore, Version=6.0.2.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35\ XPresentationFramework, Version=6.0.2.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35e aSystem.Windows.Controls.Ribbon, Version=6.0.2.0, Culture=neutral, PublicKeyToken=b77a5c561934e089R NWindowsBase, Version=6.0.2.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35F 9http://schemas.microsoft.com/winfx/2006/xaml/presentation     5      R NSystem.Xaml, Version=6.0.2.0, Culture=neutral, PublicKeyToken=b77a5c561934e089:x,http://schemas.microsoft.com/winfx/2006/xaml     5      T PMahApps.Metro, Version=3.0.0.0, Culture=neutral, PublicKeyToken=51482d6f650b2b3f;Controls,http://metro.mahapps.com/winfx/xaml/controls  5      >lvc4clr-namespace:LiveCharts.Wpf;assembly=LiveCharts.Wpf  5        � Title$=  7ArmA Reforger Server Monitor (Made by ArmAGaming.World)��5      $	��900��5      $
��1200��6$    � Icon$ Icon.ico��61    � DataContext 5	      �� 5
   
    �� RelativeSource 6   �� *Self+.��5      � .+�5   
   ]� .+�5      �� $	��Top=�6    �� Orientation$ 
Horizontal=�6.   $��10q�6G   $��Center=�6S   .+�5      �� $��	Username:��6   $��Center=�6-   $��0,0,5,0q�6H   �� 5      -   5        $��UsernameTextBox��5      $	��100��61   $	��5,0q�6=   �� 5      $��	Password:��6   $��Center=�6-   $��0,0,5,0q�6H   G� 5      -   5        $��PasswordBox��5      $	��100��61   $	��5,0q�6=   �� 5      $��Server URL:��6   $��Center=�6/   $��0,0,5,0q�6J   �� 5      -   5        $��ServerUrlTextBox��5      $	��200��62   $	��5,0q�6>   $��http://SERVERIPHERE:5000��6K   �� 5      $��
RCON Port:��6   $��Center=�6.   $��10,0,5,0q�6I   �� 5      -   5        $��RconPortTextBox��5      $��80��61   $	��5,0q�6<   $��19999��6I   �� 5      $��RCON Password:��6   $��Center=�62   $��0,0,5,0q�6M   G� 5      -   5        $��RconPasswordTextBox��5      $	��100��69   $	��5,0q�6E   �� 5      -   5        $��ConnectButton��5      $��Connect��6.   $	��5,0q�6\   �� 5      -   5        $��DisconnectButton��5      $��
Disconnect��61   $	��5,0q�6e   ${�False��6r   �� 5       -   5        $��StatusTextBlock��5       $��Center=�6K   $
��10,0q�6f   ��63   �� *Status+6f   5!      �� 5$      $��10q�6   .@�5&      �� $
��Main��6   .��5'      �� $
��Auto=�6#   .��5(      �� $��10q�6%   .+�5*      �� $ 
Horizontal=�6)   $��Center=�6B   .+�5,   "   �� $��10q�6-   .+�5-   &   �� $��Total CPU Usage��60   $
��Bold"�6G   $��Center=�6Y   #   LiveCharts.Wpf.CartesianChart  5.   &   -	   5          Name$ TotalCpuChart��5.   9   $	��250��6P   $	��200��6\   &    LiveCharts.Wpf.Charts.Base.Chart   Series 5/   )   �� *TotalCpuSeries+   AxisX 51   .      LiveCharts.Wpf.Axis     Title$	 CPU��67   	   Labels	 6C   �� *TotalCpuLabels+
   AxisY
 54   .     $ 	Usage (%)��67      MinValue$ 0Q�6I      MaxValue$	 100Q�6V      LabelFormatter 6e   �� *TotalCpuFormatter+56   '   57   #   �� 59   "   $��10q�6-   .+�5:   &   �� $��Per-Core CPU Usage��60   $
��Bold"�6J   $��Center=�6\     5;   &   -
   5        $ PerCoreCpuChart��5;   9   $	��400��6R   $	��200��6^    5<   )   �� *	CpuSeries+ 5>   .     $
 Core��67   	 6D   �� *	CpuLabels+
 5A   .     $ 	Usage (%)��67   $ 0Q�6I   $	 100Q�6V    6e   �� *CpuFormatter+5C   '   5D   #   �� 5F   "   $��10q�6-   .+�5G   &   �� $��Memory Usage (GB)��60   $
��Bold"�6I   $��Center=�6[     5H   &   -   5        $ MemoryChart��5H   9   $	��250��6N   $	��200��6Z    5I   )   �� *MemorySeries+ 5K   .     $ Memory��67   	 6F   �� *MemoryLabels+
 5N   .     $ GB��67   $ 0Q�6B    6O   �� *TotalMemoryGB+ 6r   �� *MemoryFormatter+5P   '   5Q   #   5R      �� 5T      $ 
Horizontal=�6)   $��Center=�6B   .+�5V   "   �� $��10q�6-   .+�5W   &   �� $	��FPS��60   $
��Bold"�6;   $��Center=�6M     5X   &   -   5        $ FPSChart��5X   9   $	��250��6K   $	��200��6W    5Y   )   �� *	FPSSeries+ 5[   .     $	 FPS��67   	 6C   �� *	FPSLabels+
 5^   .     $ Value��67   $ 0Q�6E   $	 100Q�6R    6a   �� *FPSFormatter+5`   '   5a   #   �� 5c   "   $��10q�6-   .+�5d   &   �� $��Frame Time (ms)��60   $
��Bold"�6G   $��Center=�6Y     5e   &   -   5        $ FrameTimeChart��5e   9   $	��250��6Q   $	��200��6]    5f   )   �� *FrameTimeSeries+ 5h   .     $ 
Frame Time��67   	 6J   �� *FrameTimeLabels+
 5k   .     $ ms��67   $ 0Q�6B   $	 100Q�6O    6^   �� *FrameTimeFormatter+5m   '   5n   #   �� 5p   "   $��10q�6-   .+�5q   &   �� $��Active Players��60   $
��Bold"�6F   $��Center=�6X     5r   &   -   5        $ ActivePlayersChart��5r   9   $	��250��6U   $	��200��6a    5s   )   �� *ActivePlayersSeries+ 5u   .     $ Players��67   	 6G   �� *ActivePlayersLabels+
 5x   .     $ Count��67   $ 0Q�6E   $	 128Q�6R    6a   �� *ActivePlayersFormatter+5z   '   5{   #   5|      �� 5~      $��10q�6)   .+�5   "   �� $!��Server Performance Summary:��6,   $
��Bold"�6O   $��14(�6a   �� 5�   "   -   5        $!��PerformanceSummaryTextBlock��5�   ,   $��12(�6s   $
��Wrap=�6�   $��0,5,0,0q�6�   ��6O   �� *PerformanceSummary+6�   5�      �� 5�      $��10q�6)   .+�5�   "   �� $��Console Log Statistics:��6,   $
��Bold"�6K   $��14(�6]   $��0,10,0,0q�6k   �� 5�   "   -   5        $ ��ConsoleLogSummaryTextBlock��5�   ,   $��12(�6q   $
��Wrap=�6   $��0,5,0,0q�6�   ��6N   �� *ConsoleLogSummary+6�   5�      5�      5�      5�      �� 5�      $��Players��6   .��5�      &   System.Windows.Controls.DataGrid  -   5        $��PlayersDataGrid��5�         AutoGenerateColumns$
 True��66   5�      �� 5�      $��Players Database��6   .��5�        -   5        $��PlayerDatabaseDataGrid��5�      $
 True��6=   5�      �� 5�      $��Raw Data��6   .��5�      �� $��10q�6!   .+�5�      �� $ 
Horizontal=�6%   $��Center=�6>   $��0,0,0,10q�6[   .+�5�      �� -   5        $��FetchRawDataButton��5�   %   $��Fetch Raw Data��6?   $��5q�6y   �� 5�      -   5        $��StopRawDataButton��5�   %   $��Stop Fetching Raw Data��6>   $��5q�6   5�      �� 5�      -   5        $��RawDataTextBox��5�   "    �� AcceptsReturn$
 True��68   " �� VerticalScrollBarVisibility$
 Auto=�6M    �� 
IsReadOnly$
 True��6p   $	��400��6�   5�      5�      �� 5�      $
��Logs��6   .��5�      � $��10q�6    � ColumnDefinitions 5�      �� 5�      $��* �6/   �� 5�      $��* �6/    � RowDefinitions 5�      �� 5�      $
��Auto �6,   �� 5�      $��* �6,   .+�5�      �� $ 
Horizontal=�6%   $��0��6>   $��0��6N   $��Center=�6[   .+�5�      �� -   5        $��FetchBackendLogsButton��5�   %   $��Fetch Backend Logs��6C   $��5q�6�   �� 5�      -   5        $��StopBackendLogsButton��5�   %   $ ��Stop Fetching Backend Logs��6B   $��5q�6�   5�      �� 5�      $ 
Horizontal=�6%   $��1��6>   $��0��6N   $��Center=�6[   .+�5�      �� -   5        $��FetchFrontendLogsButton��5�   %   $��Fetch Frontend Logs��6D   $��5q�6�   �� 5�      -   5        $��StopFrontendLogsButton��5�   %   $!��Stop Fetching Frontend Logs��6C   $��5q�6�   5�      �� 5�      -   5        $��BackendLogsTextBlock��5�   "   $��0��6>   $��1��6N   $
 True��6[   $
 Auto=�6p   $
 True��6�   $��5q�6�   �� 5�      -   5        $��FrontendLogsTextBlock��5�   "   $��1��6?   $��1��6O   $
 True��6\   $
 Auto=�6q   $
 True��6�   $��5q�6�   5�      5�      5�      5�      5�      5�      