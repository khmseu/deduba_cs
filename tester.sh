#! /bin/bash -x

rm -rfv ../ARCHIVE?/ *.tmp *.log

declare -A arexe
declare -A arts

arexe[cs]='dotnet run --project=DeDuBa'
arts[cs]='\d+\s\d\d\.\d\d\.\d\d\d\d\s\d\d:\d\d:\d\d'

arexe[pl]='./test-old.pl'
arts[pl]='\d+\s\w\w\w\s\w\w\w\s(?:\d|\s)\d\s(?:\d|\s)\d:\d\d:\d\d\s\d\d\d\d'

files="$(echo *)"
for i in cs pl; do
    rm -f "${i}."{log,tmp} "mist-${i}"
    LD_LIBRARY_PATH=/bigdata/KAI/projects/Backup/deduba_cs/OsCallsShim/bin/Debug/net8.0
    # strace -omist-$i -fvs333 -y -yy -t \#
    script -c "'time' -v  timeout 1m ${arexe[${i}]} -- ${files}" "${i}.tmp" &>/dev/null
    perl -f logfilter.pl "${arts[${i}]}" <"${i}.tmp" >"${i}.log"
done

f=artest$(date --iso).diff
rm -f artest*.diff
rm -f artest.diff
diff -aiwus {pl,cs}.log >${f}
chmod -c a-w ${f}
