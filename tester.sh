#! /bin/bash -x

rm -rfv ../ARCHIVE?/ *.tmp *.log

declare -A arexe
declare -A arts

arexe[cs]='dotnet run --project=DeDuBa'
arts[cs]='\d+\s\d\d\.\d\d\.\d\d\d\d\s\d\d:\d\d:\d\d'

arexe[pl]='./test-old.pl'
arts[pl]='\d+\s\w\w\w\s\w\w\w\s\d\d\s\d\d:\d\d:\d\d\s\d\d\d\d'

files="$(echo *)"
for i in cs pl; do
    script -c "'time' -v  ${arexe[${i}]} -- ${files}" "${i}.tmp"
    perl -f logfilter.pl "${arts[${i}]}" <"${i}.tmp" >"${i}.log"
done

f=artest$(date --iso).diff
rm -f artest*.diff
rm -f artest.diff
diff -aiwus {pl,cs}.log >${f}
chmod -c a-w ${f}
