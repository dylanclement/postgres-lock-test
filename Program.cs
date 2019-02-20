using System;
using System.Collections.Generic;
using System.Data;
using System.Threading;

using Dapper;
using Npgsql;

namespace postgres_lock_test
{   
    public class ThreadData
    {
        public int numRuns;
        public int numOrgs;
        public int maxDelayMS;
    };

    // Class to represent the MTD Service endpoint, throws an error when called with the same refresh token twice in a row
    public class HMRSService
    {
        private static Dictionary<int, string> refreshTokens = new Dictionary<int, string>();

        // NB! Mimics HMRC refresh Service call
        // - Throws exception when trying to use a refresh token that's already been used
        public static void RefreshToken(int orgId, string token)
        {
            // locking to ensure only one thread can call this at a time
            lock (refreshTokens)
            {
                if (refreshTokens.ContainsKey(orgId) && refreshTokens[orgId] == token)
                {
                    // Already refreshed :(
                    throw new Exception("Token already refreshed!");
                }

                refreshTokens[orgId] = token;
            }
        }
    }

    public class MTDService
    {   
        private static Random random = new Random();

        public static void SendMessage1(int orgId, int maxDelayMS)
        {
            using (var conn = new NpgsqlConnection("User ID=dylan.clement;Host=localhost;Port=5432;Database=postgres;Pooling=true;"))
            {
                conn.Open();

                var tran = conn.BeginTransaction();

                // Get refresh token from DB
                var id = conn.QueryFirst<string>($"SELECT RefreshToken FROM public.TestRefreshTokenLock WHERE orgId = {orgId};");

                // Get new token and sleep for some RNG
                var newToken  = random.Next().ToString();
                Thread.Sleep(random.Next(1, maxDelayMS));

                // Update refresh token in DB
                HMRSService.RefreshToken(orgId, newToken);
                conn.Execute($"UPDATE public.TestRefreshTokenLock SET RefreshToken = {newToken} WHERE orgId = {orgId};");

                // Commit transaction, release SQL locks
                tran.Commit();
            }
        }
    }

    class Program
    {
        public const int NUM_RUNS = 1000;
        public const int NUM_THREADS = 4;

        public const int NUM_ORGS = 10;
        public const int MAX_DELAY_IN_MS = 100;

        private static Random random = new Random();
    
        static void ThreadRun(object stateInfo)
        {
            var data = (ThreadData)stateInfo;
            for (var i = 0; i < data.numRuns; i++)
            {
                MTDService.SendMessage1(random.Next(0, data.numOrgs), data.maxDelayMS);
            } 
        }

        static void Main(string[] args)
        {
            for (int i = 0; i < NUM_THREADS; i++)
            {
                ThreadPool.QueueUserWorkItem(ThreadRun, new ThreadData { numOrgs = NUM_ORGS, numRuns = NUM_RUNS / NUM_THREADS, maxDelayMS = MAX_DELAY_IN_MS});
            }
            Console.WriteLine($"Successfully ran for {NUM_RUNS} iterations");       
        }
    }
}
