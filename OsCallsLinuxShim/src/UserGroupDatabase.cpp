#include "UserGroupDatabase.h"
#include <cerrno>
#include <grp.h>
#include <pwd.h>
#include <unistd.h>

namespace OsCalls {
bool handle_passwd(ValueT *value) {
  auto pw = reinterpret_cast<passwd *>(value->Handle.data1);
  switch (value->Handle.index) {
  case 0:
    if (value->Type == TypeT::IsOk) {
      set_val(String, "pw_name", pw->pw_name);
      return true;
    }
  // else fall through
  default:
    delete reinterpret_cast<passwd *>(value->Handle.data1);
    delete[] reinterpret_cast<char *>(value->Handle.data2);
    delete value;
    return false;
  case 1:
    set_val(String, "pw_passwd", pw->pw_passwd);
    return true;
  case 2:
    set_val(Number, "pw_uid", pw->pw_uid);
    return true;
  case 3:
    set_val(Number, "pw_gid", pw->pw_gid);
    return true;
  case 4:
    set_val(String, "pw_gecos", pw->pw_gecos);
    return true;
  case 5:
    set_val(String, "pw_dir", pw->pw_dir);
    return true;
  case 6:
    set_val(String, "pw_shell", pw->pw_shell);
    return true;
  }
}

bool handle_group_mem(OsCalls::ValueT *value) {
  auto mem = reinterpret_cast<char **>(value->Handle.data1);
  if (mem[value->Handle.index] == nullptr) {
    delete value;
    return false;
  }
  set_val(String, "[]", mem[value->Handle.index]);
  return true;
}

bool handle_group(ValueT *value) {
  auto gr = reinterpret_cast<group *>(value->Handle.data1);
  switch (value->Handle.index) {
  case 0:
    if (value->Type == TypeT::IsOk) {
      set_val(String, "gr_name", gr->gr_name);
      return true;
    }
  // else fall through
  default:
    delete reinterpret_cast<group *>(value->Handle.data1);
    delete[] reinterpret_cast<char *>(value->Handle.data2);
    delete value;
    return false;
  case 1:
    set_val(Number, "gr_gid", gr->gr_gid);
    return true;
  case 2:
    set_val(Complex, "gr_mem", new ValueT());
    CreateHandle(value->Complex, handle_group_mem, gr->gr_mem, nullptr);
    value->Complex->Type = TypeT::IsOk;
    return true;
  }
}

auto pwbufsz = sysconf(_SC_GETPW_R_SIZE_MAX);
auto grbufsz = sysconf(_SC_GETGR_R_SIZE_MAX);

extern "C" {
ValueT *getpwuid(int64_t uid) {
  if (pwbufsz <= 0)
    pwbufsz = 1024;
  auto pwbuf = new passwd();
  struct passwd *pwbufp = nullptr;
  auto en = 0;
  char *strbuf = nullptr;
  do {
    strbuf = new char[pwbufsz];
    en = ::getpwuid_r(uid, pwbuf, strbuf, pwbufsz, &pwbufp);
    if (en == ERANGE) {
      pwbufsz <<= 1;
      delete[] strbuf;
    }
  } while (en == ERANGE);
  auto v = new ValueT();
  CreateHandle(v, handle_passwd, pwbuf, strbuf);
  if (en == 0)
    v->Type = TypeT::IsOk;
  else
    v->Number = en;
  return v;
};

ValueT *getgrgid(int64_t gid) {
  if (grbufsz <= 0)
    grbufsz = 1024;
  auto grbuf = new group();
  struct group *grbufp = nullptr;
  auto en = 0;
  char *strbuf = nullptr;
  do {
    strbuf = new char[grbufsz];
    en = ::getgrgid_r(gid, grbuf, strbuf, grbufsz, &grbufp);
    if (en == ERANGE) {
      grbufsz <<= 1;
      delete[] strbuf;
    }
  } while (en == ERANGE);
  auto v = new ValueT();
  CreateHandle(v, handle_group, grbuf, strbuf);
  if (en == 0)
    v->Type = TypeT::IsOk;
  else
    v->Number = en;
  return v;
};
}
} // namespace OsCalls
