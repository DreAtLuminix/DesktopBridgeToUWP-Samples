﻿using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;

using System.Windows;

// The following namespace is required for BackgroundTaskBuilder APIs.
using Windows.ApplicationModel.Background;

// The following namespace is required for Registry APIs.
using Microsoft.Win32;

// The following namespace is requires to use the COM registration API (RegisterTypeForComClients).
using System.Runtime.InteropServices;
using System.Diagnostics;
using Windows.Foundation.Metadata;
using System.Threading.Tasks;


// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace BackgroundTaskWinMainComSample_CS
{
    /// <summary>
    /// An empty window that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainWindow : Window
    {
        public static void Log(string message) => 
            System.Diagnostics.Debug.WriteLine($"@@@ [{System.Threading.Thread.CurrentThread.ManagedThreadId}] {message}");

        static private uint _RegistrationToken;

        /// <summary>
        /// This method sets registry keys such that COM understands to launch
        /// the specified executable with parameters when no such process is
        /// running (to start the background task execution).
        /// 
        /// This method must be called on installation of the sparse signed
        /// package. The DesktopBridge application populates this registry key
        /// automatically upon package deployment. These COM server properties
        /// are defined in the package manifest.
        ///
        /// The process that is responsible for handling a particular background
        /// task must call RegisterProcessForBackgroundTask on the IBackgroundTask
        /// derived class. So long as this process is registered with the
        /// aforementioned API, it will be the process that has instances of the
        /// background task invoked.
        /// </summary>
        static void PopulateComRegistrationKeys(string ExecutablePath, Guid TaskClsid)
        {
            RegistryKey regKey = Registry.CurrentUser.OpenSubKey("SOFTWARE", true);
            regKey = regKey.OpenSubKey("Classes", true);
            regKey = regKey.OpenSubKey("CLSID", true);

            regKey = regKey.CreateSubKey(TaskClsid.ToString("B").ToUpper(), true);
            regKey.SetValue(null, System.IO.Path.GetFileName(ExecutablePath));

            regKey = regKey.CreateSubKey("LocalServer32", true);
            regKey.SetValue(null, ExecutablePath);

            regKey.Close();

            return;
        }

        /// <summary>
        /// This method registers the specified Trigger (for example a 15 minute
        /// timer) to start executing some app code identified by a LocalServer
        /// COM entry point.
        /// 
        /// Note that the LocalServer task entry point must be defined with COM
        /// before registering the entry point with BackgroundTaskBuilder.
        /// 
        /// This method returns either the already registered backgroudn task or
        /// a newly created background task with the specified task name.
        /// 
        /// The BackgroundTaskBuilder may throw errors when invalid parameters
        /// are passed in or the registration failed in the system for some
        /// reason.
        /// </summary>
        public IBackgroundTaskRegistration RegisterBackgroundTaskWithSystem(IBackgroundTrigger trigger, Guid entryPointClsid, string taskName)
        {
            foreach (var registrationIterator in BackgroundTaskRegistration.AllTasks)
            {
                if (registrationIterator.Value.Name == taskName)
                {
                    return registrationIterator.Value;
                }
            }

            BackgroundTaskBuilder builder = new BackgroundTaskBuilder();

            builder.SetTrigger(trigger);
            builder.SetTaskEntryPointClsid(entryPointClsid);
            builder.Name = taskName;

            BackgroundTaskRegistration registration;
            try
            {
                registration = builder.Register();
            }
            catch (Exception)
            {
                registration = null;
            }

            return registration;
        }

        /// <summary>
        /// This method unregisters all background tasks belonging to this
        /// application.
        /// </summary>
        public void UnregisterAllBackgroundTasksFromSystem(bool cancelRunningTasks)
        {
            IBackgroundTaskRegistration registration;
            foreach (var registrationIterator in BackgroundTaskRegistration.AllTasks)
            {
                registration = registrationIterator.Value;
                registration.Unregister(cancelRunningTasks);
            }
        }

        /// <summary>
        /// This method register this process as the COM server for the specified
        /// background task class until this process exits or is terminated.
        ///
        /// The process that is responsible for handling a particular background
        /// task must call RegisterTypeForComClients on the IBackgroundTask
        /// derived class. So long as this process is registered with the
        /// aforementioned API, it will be the process that has instances of the
        /// background task invoked.
        /// </summary>
        static void RegisterProcessForBackgroundTask<TaskType, TaskInterface>() where TaskType : TaskInterface, new()
        {
            // supposedly, if the application is compiled with the full .NET frameworks, the
            // built-in RegistrationServices class may be used to use COM server
            // registration APIs.
            //RegistrationServices registrationServices = new RegistrationServices();
            //registrationServices.RegisterTypeForComClients(typeof(TaskType),
            //                                               RegistrationClassContext.LocalServer,
            //                                               RegistrationConnectionType.MultipleUse);
            /*
            */

            // This app is currently using the .NET Core framework. Use the
            // manual C++ imported implementation of the CoRegisterClassObject
            // API.

            Guid taskGuid = typeof(TaskType).GUID;
            int rtn = CppComApi.CoRegisterClassObject(ref taskGuid,
                                            new CppComApi.BackgroundTaskFactory<TaskType, TaskInterface>(),
                                            CppComApi.CLSCTX_LOCAL_SERVER,
                                            CppComApi.REGCLS_MULTIPLEUSE,
                                            out _RegistrationToken);
            Log($"RegisterProcessForBackgroundTask: rtn={rtn}, _RegistrationToken={_RegistrationToken}");
            Task.Run(() => {
                Log($"RegisterProcessForBackgroundTask: forcing first run");
                TimeTriggeredTask.CalculatePrimes();
                Log($"RegisterProcessForBackgroundTask: first run stopped. real background task takes over from here");
            });
        }

        public MainWindow()
        {
            InitializeComponent();

            RegisterBackgroundTaskWithSystem(
                new TimeTrigger(TimeTriggeredTask.BACKGROUND_TASK_INTERVAL_MINUTES, false), 
                typeof(TimeTriggeredTask).GUID, 
                typeof(TimeTriggeredTask).Name
            );
            RegisterProcessForBackgroundTask<TimeTriggeredTask, IBackgroundTask>();

            dispatcherQueue = Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread();
            SimplePOCSingleton.PrimeNumberReceived += SimplePOCSingleton_PrimeNumberReceived;
        }

        Microsoft.UI.Dispatching.DispatcherQueue dispatcherQueue;

        private void SimplePOCSingleton_PrimeNumberReceived(object sender, string e)
        {
            // Log($"SimplePOCSingleton_PrimeNumberReceived: {e}");
            bool enqueued = dispatcherQueue.TryEnqueue(() =>
            {
                // UI updates here
                myButton.Content = e;
            });

            if (!enqueued)
            {
                Log("Failed to enqueue the task.");
            }
        }

        private void myButton_Click(object sender, RoutedEventArgs e)
        {
            Log("myButton_Click");
            myButton.Content = "Clicked";
        }
    }
}
