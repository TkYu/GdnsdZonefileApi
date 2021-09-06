#!/usr/bin/env sh

#MY_NS_ADDR="xxxx"
#MY_NS_KEY="xxxx"

########  Public functions #####################

#Usage: add  _acme-challenge.www.domain.com   "XKrxpRBosdIKFzxW_CT3KLZNf6q0HG9i01zxXp5CPBs"
dns_myapi_add() {
  fulldomain=$1
  txtvalue=$2

  MY_NS_ADDR="${MY_NS_ADDR:-$(_readaccountconf_mutable MY_NS_ADDR)}"
  MY_NS_KEY="${MY_NS_KEY:-$(_readaccountconf_mutable MY_NS_KEY)}"

  if [ -z "$MY_NS_ADDR" ] || [ -z "$MY_NS_KEY" ]; then
      _err "You didn't specify a Server api and key."
      return 1
  fi

  #save the api address and key to the account conf file.
  _saveaccountconf_mutable MY_NS_ADDR "$MY_NS_ADDR"
  _saveaccountconf_mutable MY_NS_KEY "$MY_NS_KEY"

  export _H1="Content-Type: application/json"
  export _H2="X-Auth-Key: $MY_NS_KEY"

  if ! _get_root "$fulldomain"; then
    _err "invalid domain"
    return 1
  fi

  _info "Adding record"

  data="{\"Serial\":\"$_nextserial\", \"hostLabel\":\"$_sub_domain\",\"recordType\":\"TXT\",\"recordData\":\"\\\"$txtvalue\\\"\"}"

  if ! response="$(_post "$data" "$MY_NS_ADDR/zone/$_domain/record?submit=true" "" "POST")"; then
      _err "Error Add <$1>"
      return 1
  fi

  _info "Added, OK"
  return 0
}

# Usage: fulldomain txtvalue
# Used to remove the txt record after validation
dns_myapi_rm() {
  fulldomain=$1
  txtvalue=$2

  MY_NS_ADDR="${MY_NS_ADDR:-$(_readaccountconf_mutable MY_NS_ADDR)}"
  MY_NS_KEY="${MY_NS_KEY:-$(_readaccountconf_mutable MY_NS_KEY)}"

  export _H1="Content-Type: application/json"
  export _H2="X-Auth-Key: $MY_NS_KEY"

  if ! _get_root "$fulldomain"; then
    _err "invalid domain"
    return 1
  fi

  _info "Removing record"
  data="{\"Serial\":\"$_nextserial\", \"hostLabel\":\"$_sub_domain\",\"recordType\":\"TXT\",\"recordData\":\"\\\"$txtvalue\\\"\"}"

  if ! response="$(_post "$data" "$MY_NS_ADDR/zone/$_domain/record/delete?submit=true" "" "POST")"; then
      _err "Error Remove <$1>"
      return 1
  fi

  _info "Removed, OK"
  return 0
}

####################  Private functions below ##################################
#_acme-challenge.www.domain.com
#returns
# _sub_domain=_acme-challenge.www
# _domain=domain.com
# _nextserial=123
_get_root() {
  domain=$1
  i=2
  p=1
  while true; do
    h=$(printf "%s" "$domain" | cut -d . -f $i-100)
    if [ -z "$h" ]; then
      #not valid
      return 1
    fi

    if _nextserial="$(_get "$MY_NS_ADDR/zone/$h/nextid")"; then
      _sub_domain=$(printf "%s" "$domain" | cut -d . -f 1-$p)
      _debug _sub_domain "$_sub_domain"
      _domain="$h"
      _debug _domain "$_domain"
      _debug _nextserial "$_nextserial"
      return 0
    fi

    p="$i"
    i=$(_math "$i" + 1)
  done
  return 1
}
