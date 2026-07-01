using System;
using System.IO;
using System.Text.RegularExpressions;

// Conventional Commits validator (https://www.conventionalcommits.org/).
// Husky passes the path to the commit message file as the first arg.
var msgPath = Args.Count > 0 ? Args[0] : null;
if (string.IsNullOrWhiteSpace(msgPath) || !File.Exists(msgPath))
{
    Console.Error.WriteLine("commit-lint: could not locate the commit message file.");
    return 1;
}

// First non-empty, non-comment line is the subject.
var subject = string.Empty;
foreach (var line in File.ReadAllLines(msgPath))
{
    if (string.IsNullOrWhiteSpace(line) || line.TrimStart().StartsWith("#"))
        continue;
    subject = line;
    break;
}

// Allow merge/revert commits through untouched.
if (subject.StartsWith("Merge ") || subject.StartsWith("Revert "))
    return 0;

// type(optional scope)(optional !): description
var pattern = @"^(build|chore|ci|docs|feat|fix|perf|refactor|revert|style|test)(\([a-z0-9\-\/]+\))?(!)?: .{1,100}$";

if (!Regex.IsMatch(subject, pattern))
{
    Console.Error.WriteLine("❌ Invalid commit message.");
    Console.Error.WriteLine($"   Subject: \"{subject}\"");
    Console.Error.WriteLine("   Expected Conventional Commits format:");
    Console.Error.WriteLine("     <type>(<optional scope>): <description>");
    Console.Error.WriteLine("   Allowed types: build, chore, ci, docs, feat, fix, perf, refactor, revert, style, test");
    Console.Error.WriteLine("   Example: feat(db): migrate EF Core provider to SQL Server");
    return 1;
}

return 0;
