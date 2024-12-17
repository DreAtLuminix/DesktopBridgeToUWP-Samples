using System;
using Windows.ApplicationModel.Background;
using System.Runtime.InteropServices;
using Windows.ApplicationModel.AppService;
using Windows.Foundation.Collections;
using System.IO.Pipes;
using System.Diagnostics;
using System.Threading;

namespace BackgroundTaskWinMainComSample_CS
{
    // The TimeTriggeredTask must be visible to COM and must be given a GUID such
    // that the system can identify this entry point and launch it as necessary
    [ComVisible(true)]
    [ClassInterface(ClassInterfaceType.None)]
    [Guid("087DC07B-FDA5-4C01-8BB7-18863C3EE597")]
    [ComSourceInterfaces(typeof(IBackgroundTask))]
    public class TimeTriggeredTask : IBackgroundTask
    {
        // This is the flag the cancellation handler signals so the Run thread may exit.
        private static volatile int cleanupTask = 0;

        private static volatile int InFirstRunFlag = 1;

        // The largest number up to which this task will calculate primes.
        private const int maxPrimeNumber = 100000;

        // the # of milliseconds to wait after calculating each prime number.
        public const int ARTIFICIAL_DELAY_MS = 10;
        public const int BACKGROUND_TASK_INTERVAL_MINUTES = 15;

        /// <summary>
        /// This method determines whether the specified number is a prime number.
        /// </summary>
        private static bool IsPrimeNumber(int dividend)
        {
            bool isPrime = true;
            for (int divisor = dividend - 1; divisor > 1; divisor -= 1)
            {
                if ((dividend % divisor) == 0)
                {
                    isPrime = false;
                    break;
                }
            }

            return isPrime;
        }

        /// <summary>
        /// This method returns the next prime number given the last calculated number.
        /// </summary>
        private static int GetNextPrime(int previousNumber)
        {
            int currentNumber = previousNumber + 1;
            while (!IsPrimeNumber(currentNumber))
            {
                currentNumber += 1;
            }

            return currentNumber;
        }

        public TimeTriggeredTask()
        {
            return;
        }

        ~TimeTriggeredTask()
        {
            return;
        }

        /// <summary>
        /// This method (declared by IBackgroundTask) is the entry point method
        /// for the sample background task. Once this method returns, the system
        /// understands that this background task has completed.
        ///
        /// This method will calculate primes up to a predefined maximum value.
        /// When the system requests this background task be canceled, the
        /// cancellation handler will set a member flag such that this thread
        /// will stop calculating primes and return.
        /// </summary>
        [MTAThread]
        public void Run(IBackgroundTaskInstance taskInstance)
        {
            MainWindow.Log("TimeTriggeredTask.Run() entering!");
            var deferral = taskInstance.GetDeferral();
            taskInstance.Canceled += OnCanceled;
            {
                int original = Interlocked.Exchange(ref InFirstRunFlag, 0);
                MainWindow.Log($"TimeTriggeredTask.Run(): Original Value: {original}, New Value: {InFirstRunFlag}");
                CalculatePrimes();
            };
            deferral.Complete();
        }


        public static void CalculatePrimes()
        {
            // Start with the first applicable number.
            int currentNumber = 1;

            // Calculate primes until a cancellation has been requested or until the maximum
            // number is reached.
            MainWindow.Log("TimeTriggeredTask: starting prime calculations");
            Stopwatch sw = new Stopwatch();
            sw.Start();
            int originallyFirstThread = InFirstRunFlag;
            while (currentNumber < maxPrimeNumber)
            {
                if (cleanupTask == 1)
                {
                    MainWindow.Log("TimeTriggeredTask: cleanupTask flag set... stopping calculation.");
                    return;
                }
                if (sw.Elapsed.TotalMinutes > BACKGROUND_TASK_INTERVAL_MINUTES)
                {
                    MainWindow.Log($"TimeTriggeredTask: greater than 15 minutes... stopping calculation.");
                    return;
                }
                // taskInstance.Progress = (uint)(currentNumber / maxPrimeNumber);

                // Compute the next prime number and add it to our queue.
                currentNumber = GetNextPrime(currentNumber);
                SimplePOCSingleton.SendIt(currentNumber.ToString());
                System.Threading.Thread.Sleep(ARTIFICIAL_DELAY_MS);

                if (originallyFirstThread == 1)
                {
                    if (InFirstRunFlag == 0)
                    {
                        // automatic one took over, need to bail this thread
                        string formTime = string.Format("{0:D2}:{1:D2}", sw.Elapsed.Minutes, sw.Elapsed.Seconds);
                        MainWindow.Log($"TimeTriggeredTask: {formTime} elapsed, automatic one took over, need to bail this thread");
                        return;
                    }
                }
            }
            sw.Stop();
            string formattedTime = string.Format("{0:D2}:{1:D2}", sw.Elapsed.Minutes, sw.Elapsed.Seconds);
            MainWindow.Log($"TimeTriggeredTask: completed prime calculations in {formattedTime}");
        }

        /// <summary>
        /// This method is the cancellation event handler method. This is called when the system requests
        /// the background task be canceled. this method will set a member flag such that the Run thread
        /// will stop calculating primes and flush the remaining values to disk.
        /// </summary>
        [MTAThread]
        public void OnCanceled(IBackgroundTaskInstance taskInstance, BackgroundTaskCancellationReason cancellationReason)
        {
            // Set the flag to indicate to the main thread that it should stop performing
            // work and exit.
            cleanupTask = 1;
            return;
        }
    }
}
