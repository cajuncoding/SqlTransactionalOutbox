using System;
using BenchmarkDotNet.Running;

namespace SqlTransactionalOutbox.Benchmarks
{
    class Program
    {
        static void Main(string[] args)
        {
            var summary = BenchmarkRunner.Run<Md5VsSha256>();
        }
    }
}
