#! /usr/bin/perl -w

use strict;
use autodie;

my $timestamp = shift @ARGV;

while (<>) {
    chomp;
    s{^$timestamp\s(<\w+\.\w+:\d+ \w+>)?\s*}{($1//'')."\n"}ge;
    s/ARCHIVE\d/ARCHIVE?/g;
    s/ => /:/;

    # s/[\%\$\@\;\\]//g;
    s/[\%\$\@\;]//g;
    s/(stat)Buf/$1/g;
    print $_, "\n";
}
