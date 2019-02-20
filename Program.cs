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
        public CountdownEvent countdownEvent;
    };

    // Class to represent the MTD Service endpoint, throws an error when called with the same refresh token twice in a row
    public class HMRSService
    {
        private static Dictionary<int, string> refreshTokens = new Dictionary<int, string>();

        // NB! Mimics HMRC refresh Service call
        // - Throws exception when trying to use a refresh token that's already been used
        public static bool RefreshToken(int orgId, string token)
        {
            // locking to ensure only one thread can call this at a time
            lock (refreshTokens)
            {
                if (refreshTokens.ContainsKey(orgId) && refreshTokens[orgId] == token)
                {
                    // Already refreshed :(
                    //Console.Write("Refresh token already used!");
                    return false;
                }

                refreshTokens[orgId] = token;

                // Returns 
                return true;
            }
        }
    }

    public class MTDService
    {   
        private static Random random = new Random();

        // No locking used for refresh tokens
        public static void SendMessage1(int orgId, int maxDelayMS, ref int failCount)
        {
            using (var conn = new NpgsqlConnection("User ID=dylan.clement;Host=localhost;Port=5432;Database=postgres;Pooling=true;"))
            {
                conn.Open();

                var tran = conn.BeginTransaction();

                // Get refresh token from DB
                var token = conn.QueryFirstOrDefault<string>($@"SELECT ""RefreshToken"" FROM ""TestRefreshTokenLock"" WHERE ""OrgId"" = {orgId};");

                // Get new token and sleep for some RNG
                var newToken  = random.Next().ToString();
                Thread.Sleep(random.Next(1, maxDelayMS));

                // Update refresh token in DB
                if (HMRSService.RefreshToken(orgId, newToken) == false) {
                    // Tried to re-use refresh token
                    Interlocked.Increment(ref failCount);

                }
                conn.Execute($@"UPDATE ""TestRefreshTokenLock"" SET ""RefreshToken"" = '{newToken}' WHERE ""OrgId"" = {orgId};");

                // Commit transaction, release SQL locks
                tran.Commit();
            }
        }

        
        // Using update locks used for refresh tokens
        public static void SendMessage2(int orgId, int maxDelayMS, ref int failCount)
        {
            using (var conn = new NpgsqlConnection("User ID=dylan.clement;Host=localhost;Port=5432;Database=postgres;Pooling=true;"))
            {
                conn.Open();

                var tran = conn.BeginTransaction();

                // Get refresh token from DB
                var token = conn.QueryFirstOrDefault<string>($@"SELECT ""RefreshToken"" FROM ""TestRefreshTokenLock"" WHERE ""OrgId"" = {orgId} FOR UPDATE;");

                // Get new token and sleep for some RNG
                var newToken  = random.Next().ToString();
                Thread.Sleep(random.Next(1, maxDelayMS));

                // Update refresh token in DB
                if (HMRSService.RefreshToken(orgId, newToken) == false) {
                    // Tried to re-use refresh token
                    Interlocked.Increment(ref failCount);
                }
                conn.Execute($@"UPDATE ""TestRefreshTokenLock"" SET ""RefreshToken"" = '{newToken}' WHERE ""OrgId"" = {orgId};");

                // Commit transaction, release SQL locks
                tran.Commit();
            }
        }
    }

    class Program
    {
        public const int NUM_RUNS = 1000;
        public const int NUM_THREADS = 32;
        public const int NUM_ORGS = 5;
        public const int MAX_DELAY_IN_MS = 10;
        private static int failCount = 0;
        private static Random random = new Random();
    
        static void ThreadRun(object stateInfo)
        {
            var data = (ThreadData)stateInfo;
            for (var i = 0; i < data.numRuns; i++)
            {
                // Change send message function here
                MTDService.SendMessage1(random.Next(0, data.numOrgs), data.maxDelayMS, ref failCount);
            }

            // Signal event
            data.countdownEvent.Signal();
        }

        static void Main(string[] args)
        {
            // create countdown event to wait fo threads to exit
            using (var countdownEvent = new CountdownEvent(NUM_THREADS))
            {
                for (int i = 0; i < NUM_THREADS; i++)
                {
                    ThreadPool.QueueUserWorkItem(ThreadRun, new ThreadData { numOrgs = NUM_ORGS, numRuns = NUM_RUNS / NUM_THREADS, maxDelayMS = MAX_DELAY_IN_MS, countdownEvent = countdownEvent});
                }

                // Wait for all threads to complete
                countdownEvent.Wait();
                Console.WriteLine($"Successfully ran for {NUM_RUNS} iterations with {failCount} errors." );   
            }    
        }
    }
}
