using System;
using System.Collections.Generic;
using System.Threading;

namespace postgres_lock_test
{   
    public class ThreadData
    {
    };

    // Class to represent the MTD Service endpoint, throws an error when called with the same refresh token twice in a row
    public class MTDService
    {
        private static Dictionary<int, string> refreshTokens = new Dictionary<int, string>();

        private static Random random = new Random();
        public static void RefreshToken(int orgId, string token, int maxDelayMS)
        {
            lock (refreshTokens)
            {
                if (refreshTokens.ContainsKey(orgId) && refreshTokens[orgId] == token)
                {
                    // Already refreshed :(
                    throw new Exception("Token already refreshed!");
                }

                // Add some RNG
                Thread.Sleep(random.Next(1, maxDelayMS));
                refreshTokens[orgId] = token;
            }
        }
    }

    public class RefreshRepository
    {

    }

    class Program
    {
        public const int NUM_RUNS = 1000;
        public const int NUM_THREADS = 4;

        public const int NUM_ORGS = 10;
        public int MAX_DELAY_IN_MS = 100;

        private static Random random = new Random();
    
        static void ThreadRun(int numRuns, int numOrgs, int maxDelayMS)
        {
            HMRSService.RefreshToken(random.Next(0, numOrgs), random.Next().ToString(), maxDelayMS);
        }

        static void Main(string[] args)
        {
            for (int i = 0; i < NUM_THREADS; i++)
            {

            }
        }
    }
}
