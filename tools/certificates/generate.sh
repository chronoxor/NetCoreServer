#!/bin/bash

# Certificate Authority (CA)
openssl genrsa -passout pass:qwerty -out ca-secret.key 4096
openssl rsa -passin pass:qwerty -in ca-secret.key -out ca.key
openssl req -new -x509 -days 3650 -subj '/C=BY/ST=Belarus/L=Minsk/O=Example root CA/OU=Example CA unit/CN=example.com' -key ca.key -out ca.crt
openssl pkcs12 -clcerts -export -passout pass:qwerty -in ca.crt -inkey ca.key -out ca.p12
openssl pkcs12 -clcerts -passin pass:qwerty -passout pass:qwerty -in ca.p12 -out ca.pem
openssl pkcs12 -export -out ca.pfx -inkey ca.key -in ca.crt

# SSL Server certificate
openssl genrsa -passout pass:qwerty -out server-secret.key 4096
openssl rsa -passin pass:qwerty -in server-secret.key -out server.key
openssl req -new -subj '/C=BY/ST=Belarus/L=Minsk/O=Example server/OU=Example server unit/CN=server.example.com' -key server.key -out server.csr
openssl x509 -req -days 3650 -in server.csr -CA ca.crt -CAkey ca.key -set_serial 01 -out server.crt
openssl pkcs12 -clcerts -export -passout pass:qwerty -in server.crt -inkey server.key -out server.p12
openssl pkcs12 -clcerts -passin pass:qwerty -passout pass:qwerty -in server.p12 -out server.pem
openssl pkcs12 -export -out server.pfx -inkey server.key -in server.crt

# SSL Client certificate
openssl genrsa -passout pass:qwerty -out client-secret.key 4096
openssl rsa -passin pass:qwerty -in client-secret.key -out client.key
openssl req -new -subj '/C=BY/ST=Belarus/L=Minsk/O=Example client/OU=Example client unit/CN=client.example.com' -key client.key -out client.csr
openssl x509 -req -days 3650 -in client.csr -CA ca.crt -CAkey ca.key -set_serial 01 -out client.crt
openssl pkcs12 -clcerts -export -passout pass:qwerty -in client.crt -inkey client.key -out client.p12
openssl pkcs12 -clcerts -passin pass:qwerty -passout pass:qwerty -in client.p12 -out client.pem
openssl pkcs12 -export -out client.pfx -inkey client.key -in client.crt

# Diffie–Hellman (D-H) key exchange
openssl dhparam -out dh4096.pem 4096
