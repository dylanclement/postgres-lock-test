# postgres-lock-test

App to replicate race condition when refreshing MTD tokens, and seeing if we can use postgres to lock while refreshing.

# Installation
1) git clone the repo
2) dotnet restore
3) Update connection string in code
4) Run postgres script to create table
5) dotnet run

Example Output> Successfully ran for 1000 iterations with 5 errors.

TODO!
 - Fix sendmessage2 to lock
 - Need to run with 0 errors.
