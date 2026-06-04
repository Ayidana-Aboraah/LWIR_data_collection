// Copyright (c) 2008-2025 Optris GmbH & Co. KG

using Optris.OtcSDK;


namespace SimpleViewCS
{
    internal static class Program
    {
        /// <summary>Program main entry point.</summary>
        [STAThread]
        static void Main()
        {
            // To customize application configuration such as set high DPI settings or default font,
            // see https://aka.ms/applicationconfiguration.
            ApplicationConfiguration.Initialize();

            // Initialize the SDK by setting log verbosity
            Sdk.init(Verbosity.Off, Verbosity.Off, "SimpleViewCS");

            /*
             * Add an additional detector for Ethernet devices on the network 192.168.0.0/24. A detector for 
             * USB devices was added when calling Sdk.init().
             */
            EnumerationManager.getInstance().addEthernetDetector("192.168.0.0/24");

            try
            {
                Application.Run(new DisplayForm());
            }
            catch (SDKException ex)
            {
                MessageBox.Show(ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}