﻿using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using IocPerformance.Adapters;

namespace IocPerformance.Benchmarks
{
    public class MultithreadedBenchmarkMeasurer : BenchmarkMeasurer
    {
        private const int NumberOfThreads = 2;

        public MultithreadedBenchmarkMeasurer(IContainerAdapter container, IBenchmark benchmark) : base(container, benchmark)
        {
        }

        public override Measurement Measure()
        {
            this.CollectMemory();

            var watch = new Stopwatch();
            var result = new Measurement();

            const int Loopcount = Benchmarks.Benchmark.LoopCount / NumberOfThreads;
            var counter = 0;
            Exception exception = null;

            var threads = Enumerable.Range(0, NumberOfThreads)
                .Select(i => new Thread(() =>
                {
                    try
                    {
                        for (var j = 0; j < Loopcount; j++)
                        {
                            Interlocked.Increment(ref counter);
                            Benchmark.MethodToBenchmark(Container);

                            // If measurement takes more than three minutes, stop and interpolate result
                            if (result.ExtraPolated || (i % 500 == 0 && watch.ElapsedMilliseconds > TimeLimit))
                            {
                                watch.Stop();
                                result.ExtraPolated = true;
                                break;
                            }

                            if (exception != null)
                            {
                                break;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        this.CollectMemory();

                        exception = ex;
                    }
                }))
                .ToList();

            watch.Start();

            foreach (var thread in threads)
            {
                thread.Start();
            }

            foreach (var thread in threads)
            {
                thread.Join();
            }

            if (exception != null)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine(
                    " Benchmark '{0}' (multiple threads) failed: {1}",
                    Benchmark.Name,
                    exception.Message);
                Console.ResetColor();

                result.Error = exception is OutOfMemoryException ? "OoM" : "Error";
            }
            else if (result.ExtraPolated)
            {
                result.Time = watch.ElapsedMilliseconds * Benchmarks.Benchmark.LoopCount / counter;

                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine(
                    " Benchmark '{0}' (multiple threads) was stopped after {1:f1} minutes. About {2} of {3} instances have been resolved. Total execution would have taken: {4:f1} minutes.",
                    Benchmark.Name,
                    (double)watch.ElapsedMilliseconds / (1000 * 60),
                    counter,
                    Benchmarks.Benchmark.LoopCount,
                    (double)result.Time / (1000 * 60));
                Console.ResetColor();

                return result;
            }

            watch.Stop();

            if (result.Error == null)
            {
                result.Time = watch.ElapsedMilliseconds;
            }

            return result;
        }
    }
}