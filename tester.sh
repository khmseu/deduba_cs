#! /bin/bash -x

rm -rfv ../ARCHIVE?/ *.tmp *.log

declare -A arexe
declare -A arts

arexe[cs]='dotnet run --project=DeDuBa'
arts[cs]='\d+\s\d\d\.\d\d\.\d\d\d\d\s\d\d:\d\d:\d\d'

arexe[pl]='./deduba.pl'
arts[pl]='\d+\s\w\w\w\s\w\w\w\s\d\d\s\d\d:\d\d:\d\d\s\d\d\d\d'

for i in cs pl; do
    script -c "'time' -v $(echo ${arexe[$i]} *)" "${i}.tmp"
    perl -pe "s,${arts[$i]}\s*,,g; s,ARCHIVE\d,ARCHIVE?,g" <"${i}.tmp" >"${i}.log"
done
