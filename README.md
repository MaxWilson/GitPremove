# GitPremove
Make git conflict resolution easier and history cleaner, by using this tool to do all file moves before a merge or rebase in a separate commit

Usage: use git worktree to check out two versions of a repo at the same time in different repos. Let's say you've got the following repo with conflicts:

mkdir c:\code\repro\rebaseIssue
cd c:\code\repro\rebaseIssue
git init
echo "line 1" > a.txt
echo "dog" > b.txt
git add .
git commit -m "Baseline"
git checkout -b branch1
mkdir src
git mv a.txt src\a1.txt
git commit -m "Move a1"
git mv b.txt src\b1.txt
git commit -m "Move b1"
echo horse > src\b1.txt
git commit -am "Change dog to horse"
git checkout master
git checkout -b branch2
echo pigeon > b.txt
git commit -am "Change line 1 to line 2"

Now, branch 1 and branch2 have a conflict. If you try to merge them, 

git checkout branch2
git merge branch1
REM you'll have merge conflicts here
REM renamed:    a.txt -> src/a1.txt
REM new file:   src/b1.txt
REM 
REM Unmerged paths:
REM (use "git add/rm <file>..." as appropriate to mark resolution)
REM     deleted by them: b.txt
REM resolve them by
xcopy b.txt src\b1.txt
erase b.txt
git add .
git commit

At this point I was expecting git to be confused and not realize that b had been renamed instead of deleted + added, but... currently git is picking it up
correctly. 