#! /usr/bin/perl -w

use 5.018;
no warnings qw( deprecated::smartmatch experimental::smartmatch );

use Data::Dumper::Simple;
$Data::Dumper::Useqq    = 1;
$Data::Dumper::Sortkeys = 1;
$Data::Dumper::Deparse  = 1;

$| = 1;

use Carp qw( confess );
use Cwd  qw( realpath );
use DB_File;
use Digest::SHA             qw( sha512_hex );
use File::Path              qw( make_path remove_tree );
use File::Spec::Functions   qw( :ALL );
use IO::Compress::Bzip2     qw( :all );
use IO::Uncompress::Bunzip2 qw( :all );
use IO::Compress::Xz        qw( :all );
use IO::Uncompress::UnXz    qw( :all );
use POSIX                   qw( strftime );

$SIG{__WARN__} = sub () { confess @_; };
$SIG{__DIE__}  = sub () { confess @_; };

use constant CHUNKSIZE => 1024 * 1024 * 1024;
use constant START_TIMESTAMP => strftime '%Y-%m-%d-%H-%M-%S', localtime;

use constant TESTING => 1;
my $archive =
  TESTING ? '/home/kai/projects/Backup/ARCHIVE2' : '/archive/backup';
my $data_path = catfile $archive, 'DATA';
my $tmpp      = catfile $archive, "tmp.$$";

my %settings;
my $ds;
my %dirtmp;
my %bstats;
my %devices;
my %fs2ino;
my $packsum;

sub error($$);

make_path(
    $data_path,
    {
        verbose => 1,
        mode    => 0711,
    }
);

my $logname = catfile $archive, 'log_' . START_TIMESTAMP;
open my $LOG, '>', $logname or error $logname, 'open';
STDOUT->autoflush(1);
STDERR->autoflush(1);
$LOG->autoflush(1);

##############################################################################
# Temporary on-disk hashes for backup data management
##############################################################################
# arlist: hash -> part of filename between $data_path and actual file
my %arlist;

# preflist: list of files and directories under a given prefix
# (as \0-separated list)
my %preflist;

##############################################################################
# Subroutines
##############################################################################

##############################################################################
# errors

sub error($$);

sub error($$) {
    my ( $file, $op ) = @_;
    my $msg = "*** $file: $op: $!\n";
    print "\n", __LINE__, ' ', scalar localtime, ' ', $msg if TESTING;
    if ( defined $LOG ) {
        print $LOG $msg;
    }
    else {
        die;
    }
}

##############################################################################
# build arlist/preflist

sub mkarlist(@);

sub mkarlist(@) {
    for my $entry ( sort @_ ) {
        if ( $entry eq $data_path ) {
            print "\n+ $entry" if TESTING;
            my $DIR;
            unless ( opendir $DIR, $entry ) {
                error $entry, 'opendir';
                next;
            }
            my @entries = readdir $DIR;
            unless ( closedir $DIR ) {
                error $entry, 'closedir';
                next;
            }
            print "\t", ( scalar @entries ), " entries" if TESTING;
            mkarlist map { "$entry/$_" } grep !/^\.\.?$/, @entries;
            print "\tdone $entry\n" if TESTING;
            next;
        }
        my ( $prefix, $file ) = ( $entry =~ m{^\Q$data_path\E/?(.*)/([^/]+)$} );
        if ( $file =~ /^[0-9a-f][0-9a-f]$/ ) {
            print "\n+ $entry:$prefix:$file" if TESTING;
            $preflist{$prefix} .= "$file/\0";
            my $DIR;
            unless ( opendir $DIR, $entry ) {
                error $entry, 'opendir';
                next;
            }
            my @entries = readdir $DIR;
            unless ( closedir $DIR ) {
                error $entry, 'closedir';
                next;
            }
            print "\t", ( scalar @entries ), " entries" if TESTING;
            mkarlist map { "$entry/$_" } grep !/^\.\.?$/, @entries;
            print "\tdone $entry\n" if TESTING;
        }
        elsif ( $file =~ /^[0-9a-f]+$/ ) {
            $arlist{$file} = $prefix;
            $preflist{$prefix} .= "$file\0";
        }
        else {
            warn "Bad entry in archive: $entry";
        }
    }
}

##############################################################################
# find place for hashed file, or note we already have it

sub hash2fn($);

sub hash2fn($) {
    my $hash = shift;
    print "\n", __LINE__, ' ', scalar localtime, ' ', Dumper($hash) if TESTING;
    if ( exists $arlist{$hash} ) {
        $packsum += -s "$data_path/$arlist{$hash}/$hash";
        return undef;
    }
    else {
        my $prefix = $hash;
        my @prefix = grep /\S/, split /(..)/, $prefix;
        pop @prefix;
        while (@prefix) {
            $prefix = join '/', @prefix;
            last if exists $preflist{$prefix};
            pop @prefix;
        }
        $prefix = join '/', @prefix unless @prefix;
        my @list  = split /\0/, $preflist{$prefix};
        my $nlist = scalar @list;
        if ( 255 < $nlist ) {

            # dir becoming too large, move files into subdirs
            print "\n", __LINE__, ' ', scalar localtime, ' ',
              "*** reorganizing '$prefix' [$nlist entries]\n";
            print "\n", __LINE__, ' ', scalar localtime, ' ', Dumper(@list)
              if TESTING;
            my $depth = scalar @prefix;
            my $plen  = 2 * $depth;
            my %new;
            for my $f (@list) {
                if ( $f =~ m[/$] ) {    #]){#
                    $new{$f}++;
                }
            }
            for my $n ( 0x00 .. 0xff ) {
                my $dir = sprintf "%02x", $n;
                my $de  = "$dir/";
                unless ( exists $new{$de} ) {
                    mkdir "$data_path/$prefix/$dir";
                    $new{$de}++;
                    $preflist{"$prefix/$dir"} = '';
                }
            }
            for my $f (@list) {
                unless ( $f =~ m[/$] ) {    #]){#
                    my $dir = substr( $f, $plen, 2 );
                    my $de  = "$dir/";
                    unless ( exists $new{$de} ) {
                        mkdir "$data_path/$prefix/$dir";
                        $new{$de}++;
                    }
                    my ( $from, $to ) = (
                        "$data_path/$prefix/$f", "$data_path/$prefix/$dir/$f"
                    );
                    unless ( rename $from, $to ) {
                        error "$from -> $to", 'rename';
                        next;
                    }
                    my $newpfx = "$prefix/$dir";
                    $arlist{$f} = $newpfx;
                    $preflist{$newpfx} .= "$f\0";
                }
            }
            $preflist{$prefix} = join "\0", keys %new, '';
            my $dir = substr( $hash, $plen, 2 );
            $prefix = "$prefix/$dir";

#			print "\n", __LINE__, ' ', scalar localtime, ' ',  "After reorg:\n" if TESTING;
#			while (my ($k, $v) = each %arlist) {
#				print "\n", __LINE__, ' ', scalar localtime, ' ',  Data::Dumper->Dump([$v], ["\$arlist{'$k'}"]) if TESTING;
#			}
#			while (my ($k, $v) = each %preflist) {
#				print "\n", __LINE__, ' ', scalar localtime, ' ',  Data::Dumper->Dump([$v], ["\$preflist{'$k'}"]) if TESTING;
#			}
#			print "\n", __LINE__, ' ', scalar localtime, ' ',  "\n" if TESTING;
        }
        else {
            print "\n", __LINE__, ' ', scalar localtime, ' ',
              "+++ not too large: '$prefix' entries = ", scalar @list, "\n"
              if TESTING;
        }
        $arlist{$hash} = $prefix;
        $preflist{$prefix} .= "$hash\0";
        return "$data_path/$prefix/$hash";
    }
}

##############################################################################
# Structured data
#
# unpacked: [ ... [ ... ] ... 'string' \number ... ]
#
# packed: w/a strings, w/w unsigned numbers, w/(a) lists
#

sub sdpack($$);

sub sdpack($$) {
    my $v    = shift;
    my $t    = ref $v;
    my $name = shift;
    die unless defined $name;
    print "\n", __LINE__, ' ', scalar localtime, ' ', $name, ': ',
      Dumper( $t, $v )
      if length $name and TESTING;
    given ($t) {
        when ( [''] ) {
            return defined $v ? 's' . $v : 'u';
        }
        when ( [qw{ SCALAR }] ) {
            given ($$v) {
                when (/^\d+$/) {
                    return 'n' . pack 'w', $$v;
                }
                when (/^-\d+$/) {
                    return 'N' . pack 'w', -$$v;
                }
                default {
                    die 'not a number ' . $$v;
                }
            }
        }
        when ( [qw{ ARRAY }] ) {
            my @ary = map { sdpack $_, '' } @$v;
            return 'l' . pack 'w/(w/a)', @ary;
        }
        when ( [qw{ HASH CODE REF GLOB LVALUE FORMAT IO VSTRING Regexp }] ) {
            die 'unexpected type ' . $t;
        }
        default {
            die 'unexpected type ' . $t;
        }
    }
}

sub sdunpack($);

sub sdunpack($) {
    my $v = shift;
    my ( $p, $d ) = unpack 'a1 a*', $v;
    given ($p) {
        when ( [qw{ u }] ) {
            return undef;
        }
        when ( [qw{ s }] ) {
            return $d;
        }
        when ( [qw{ n }] ) {
            return \unpack 'w', $d;
        }
        when ( [qw{ N }] ) {
            return \-unpack 'w', $d;
        }
        when ( [qw{ l }] ) {
            return [ map { sdunpack $_ } unpack 'w/(w/a)', $d ];
        }
        default {
            die 'unexpected type ' . $p;
        }
    }
}

sub usr($) {
    my $v = shift;
    return [ \$v, scalar getpwuid $v ];
}

sub grp($) {
    my $v = shift;
    return [ \$v, scalar getgrgid $v ];
}

sub save_data($) {
    my ($data) = @_;
    my $hash   = sha512_hex $data;
    my $out    = hash2fn $hash;
    if ( defined $out ) {
        $bstats{saved_blocks}++;
        $bstats{saved_bytes} += length $data;
        unless ( my $status = bzip2 \$data => $out ) {
            error $out, "bzip2 failed: $Bzip2Error";
            $packsum += -s $out;
            return $hash;    # ???
        }
        $packsum += -s $out;
        print "\n", __LINE__, ' ', scalar localtime, ' ', $hash, "\n"
          if TESTING;
    }
    else {
        $bstats{duplicate_blocks}++;
        $bstats{duplicate_bytes} += length $data;
        print "\n", __LINE__, ' ', scalar localtime, ' ', $hash,
          " already exists\n"
          if TESTING;
    }
    return $hash;
}

sub save_file(*$$) {
    my $file = shift;
    my $size = shift;
    my $tag  = shift;
    my @hashes;
    print "\n", __LINE__, ' ', scalar localtime, ' save_file: ',
      Dumper( $size, $tag )
      if TESTING;

#my @layers = PerlIO::get_layers($file, details => 1);
#print "\n", __LINE__, ' ', scalar localtime, ' input: ', Dumper(@layers, $size) if TESTING;
    while ( $size > 0 ) {
        my $data = undef;
        my $n1   = read $file, $data, CHUNKSIZE;
        my $e    = $!;
        my $n2   = length $data;
        print "\n", __LINE__, ' ', scalar localtime, ' chunk: ',
          Dumper( $size, $n1, $n2 )
          if TESTING;
        unless ( defined $n1 ) {
            $! = $e;
            error $tag, 'read';
        }
        last unless $n1;
        push @hashes, save_data $data;
        $size -= $n2;
        $ds   += $n2;
        $data = undef;
    }
    print "\n", __LINE__, ' ', scalar localtime, ' eof: ',
      Dumper( $size, @hashes )
      if TESTING;
    return @hashes;
}

##############################################################################
sub backup_worker(@);

sub backup_worker(@) {
    my @sorted = sort @_;
    print Dumper( @_, @sorted );
    for my $entry ( sort @_ ) {
        my ( $volume, $directories, $file ) = splitpath $entry, 0;
        print "\n", __LINE__, ' ', scalar localtime, ' ', "=" x 80, "\n"
          if TESTING;
        print "\n", __LINE__, ' ', scalar localtime, ' ',
          Dumper( $entry, $volume, $directories, $file )
          if TESTING;
        my $dir  = catfile $volume, $directories;
        my $name = $file;
        my @stat = lstat $entry;
        my $e    = $!;

        # $dir is the current directory name,
        # $name is the current filename within that directory
        # $entry is the complete pathname to the file.
        my $start = time;
        print "\n", __LINE__, ' ', scalar localtime, ' handle_file: ',
          Dumper( $dir, $name, $entry )
          if TESTING;
        unless (@stat) {
            $! = $e;
            error $entry, 'lstat';
            next;
        }
        print "\n", __LINE__, ' ', scalar localtime, ' ', Dumper( $stat[0] )
          if TESTING;
        if ( $devices{ $stat[0] }
            and abs2rel( $entry, $archive ) =~ m[^\.\.?/] )
        {
            print "\n", __LINE__, ' ', scalar localtime, ' stat: ',
              Dumper(@stat)
              if TESTING;

# 0 dev      device number of filesystem
# 1 ino      inode number
# 2 mode     file mode  (type and permissions)
# 3 nlink    number of (hard) links to the file
# 4 uid      numeric user ID of file's owner
# 5 gid      numeric group ID of file's owner
# 6 rdev     the device identifier (special files only)
# 7 size     total size of file, in bytes
# 8 atime    last access time in seconds since the epoch
# 9 mtime    last modify time in seconds since the epoch
# 10 ctime    inode change time in seconds since the epoch (*)
# 11 blksize  preferred I/O size in bytes for interacting with the file (may vary from file to file)
# 12 blocks   actual number of system-specific blocks allocated on disk (often, but not always, 512 bytes each)
            my $fsfid = sdpack [ \$stat[0], \$stat[1] ], 'fsfid';
            my $old   = exists $fs2ino{$fsfid};
            my $report;
            if ( not $old ) {
                $fs2ino{$fsfid} = sdpack undef, '';
                if ( -d _ ) {
                    for ( ; ; ) {
                        my $DIR;
                        unless ( opendir $DIR, $entry ) {
                            error $entry, 'opendir';
                            last;
                        }
                        my @entries = readdir $DIR;
                        unless ( closedir $DIR ) {
                            error $entry, 'closedir';
                            last;
                        }
                        backup_worker map { catfile $entry, $_ }
                          grep !/^\.\.?$/, @entries;
                        last;
                    }
                }
                $packsum = 0;
                lstat $entry;
                my @inode = [
                    ( map { \$_ } @stat[ 2, 3 ] ),
                    usr $stat[4],
                    grp $stat[5],
                    ( map { \$_ } @stat[ 6, 7, 9, 10 ] )
                ];
                my ( $data, @hashes );
                $ds = 0;
                if ( -f _ ) {
                    my $size = -s _;
                    if ($size) {
                        my $file;
                        print "\n", __LINE__, ' ', scalar localtime, ' ',
                          Dumper($entry)
                          if TESTING;
                        unless ( open $file, '<:unix mmap raw', $entry ) {
                            error $entry, 'open';
                            next;
                        }
                        @hashes = save_file $file, $size, $entry;
                    }
                }
                elsif ( -l _ ) {
                    $data = readlink $entry;
                    unless ( defined $data ) {
                        error $entry, 'readlink';
                        next;
                    }
                    my $size = length $data;
                    print "\n", __LINE__, ' ', scalar localtime, ' ',
                      Dumper($data)
                      if TESTING;
                    my $mem;
                    unless ( open $mem, '<', \$data ) {
                        error $entry, 'open $data readlink';
                        next;
                    }

                   #open my $mem, '<:unix mmap raw', \$data or die "\$data: $!";
                    @hashes = save_file $mem, $size, "$entry \$data readlink";
                    $ds     = length $data;
                    $data   = undef;
                }
                elsif ( -d _ ) {
                    my $data = sdpack $dirtmp{$entry} || [], 'dir';
                    delete $dirtmp{$entry};
                    my $size = length $data;
                    print "\n", __LINE__, ' ', scalar localtime, ' ',
                      Dumper($data)
                      if TESTING;
                    my $mem;
                    unless ( open $mem, '<', \$data ) {
                        error $entry, 'open $data $dirtmp';
                        next;
                    }

                   #open my $mem, '<:unix mmap raw', \$data or die "\$data: $!";
                    @hashes = save_file $mem, $size, "$entry \$data \$dirtmp";
                    $ds     = length $data;
                    $data   = undef;
                }
                print "\n", __LINE__, ' ', scalar localtime, ' data: ',
                  Dumper(@hashes)
                  if TESTING;
                push @inode, [@hashes];
                $data = sdpack [@inode], 'inode';
                print "\n", __LINE__, ' ', scalar localtime, ' ', Dumper($data)
                  if TESTING;
                my $mem;
                unless ( open $mem, '<', \$data ) {
                    error $entry, 'open $data @inode';
                    next;
                }

            #open my $mem, '<:unix mmap raw scalar', \$data or die "\$data: $!";
                @hashes = save_file $mem, length $data, "$entry \$data \@inode";
                my $ino = sdpack [@hashes], 'fileid';
                $fs2ino{$fsfid} = $ino;
                my $needed = time - $start;
                my $speed  = $needed ? $ds / $needed : undef;
                print "\n", __LINE__, ' ', scalar localtime, ' timing: ',
                  Dumper( $ds, $needed, $speed )
                  if TESTING;
                $report = sprintf '[%d -> %d: %ds]', $stat[7], $packsum,
                  $needed;
            }
            else {
                $report = sprintf '[%d -> duplicate]', $stat[7];
            }
            push @{ $dirtmp{$dir} }, [ $name, $fs2ino{$fsfid} ];
            print $LOG unpack( 'H*', $fs2ino{$fsfid} ), " $entry $report\n";
            print "\n", __LINE__, ' ', scalar localtime, ' ',
              unpack( 'H*', $fs2ino{$fsfid} ), " $entry $report\n"
              if TESTING;
        }
        else {
            error $entry, 'pruning';
        }
        print "\n", __LINE__, ' ', scalar localtime, ' ', "_" x 80, "\n"
          if TESTING;
    }
}

##############################################################################
# Main program
##############################################################################
print "\n\nMain program\n";

@ARGV = map { canonpath realpath $_ } @ARGV;
print "Filtered:\n";
print Dumper(@ARGV);

for my $root (@ARGV) {
    my @st = lstat $root;
    $devices{ $st[0] }++ if @st;
}
print "\n", __LINE__, ' ', scalar localtime, ' ', Dumper(%devices);

##############################################################################

tie %arlist,   'DB_File', undef;    #, "$tmpp.arlist";
tie %preflist, 'DB_File', undef;    #, "$tmpp.preflist";
$preflist{''} = '';

print "\n", __LINE__, ' ', scalar localtime, ' ', "Getting archive state\n";

mkarlist $data_path;

if (TESTING) {
    print "\n", __LINE__, ' ', scalar localtime, ' ', "Before backup:\n";
    while ( my ( $k, $v ) = each %arlist ) {
        print "\n", __LINE__, ' ', scalar localtime, ' ',
          Data::Dumper->Dump( [$v], ["\$arlist{'$k'}"] );
    }
    while ( my ( $k, $v ) = each %preflist ) {
        print "\n", __LINE__, ' ', scalar localtime, ' ',
          Data::Dumper->Dump( [$v], ["\$preflist{'$k'}"] );
    }
    print "\n", __LINE__, ' ', scalar localtime, ' ', "\n";
}

##############################################################################

print "\n", __LINE__, ' ', scalar localtime, ' ', "Backup starting\n";

backup_worker @ARGV;

##############################################################################

#my $status = unxz $input => $output [,OPTS] or print "\n", __LINE__, ' ', scalar localtime, ' ',  "\n", __ "unxz failed: $UnXzError\n" if TESTING;

print "\n", __LINE__, ' ', scalar localtime, ' ', Dumper(%dirtmp) if TESTING;

print "\n", __LINE__, ' ', scalar localtime, ' ', Dumper(%devices) if TESTING;

print "\n", __LINE__, ' ', scalar localtime, ' ', Dumper(%bstats);

untie %arlist;
untie %preflist;
unlink "$tmpp.arlist", "$tmpp.preflist";

close $LOG or error $logname, 'close';

print "\n", __LINE__, ' ', scalar localtime, ' ', "Backup done\n";
