@echo off

rem Certificate Authority (CA)
openssl genrsa -passout pass:qwerty -out ca-secret.key 4096
openssl rsa -passin pass:qwerty -in ca-secret.key -out ca.key
openssl req -new -x509 -days 3650 -subj "/C=BY/ST=Belarus/L=Minsk/O=Example root CA/OU=Example CA unit/CN=example.com" -key ca.key -out ca.crt
openssl pkcs12 -export -passout pass:qwerty -inkey ca.key -in ca.crt -out ca.pfx
openssl pkcs12 -passin pass:qwerty -passout pass:qwerty -in ca.pfx -out ca.pem

rem SSL Server certificate
openssl genrsa -passout pass:qwerty -out server-secret.key 4096
openssl rsa -passin pass:qwerty -in server-secret.key -out server.key
openssl req -new -subj "/C=BY/ST=Belarus/L=Minsk/O=Example server/OU=Example server unit/CN=server.example.com" -key server.key -out server.csr
openssl x509 -req -days 3650 -in server.csr -CA ca.crt -CAkey ca.key -set_serial 01 -out server.crt
openssl pkcs12 -export -passout pass:qwerty -inkey server.key -in server.crt -out server.pfx
openssl pkcs12 -passin pass:qwerty -passout pass:qwerty -in server.pfx -out server.pem

rem SSL Client certificate
openssl genrsa -passout pass:qwerty -out client-secret.key 4096
openssl rsa -passin pass:qwerty -in client-secret.key -out client.key
openssl req -new -subj "/C=BY/ST=Belarus/L=Minsk/O=Example client/OU=Example client unit/CN=client.example.com" -key client.key -out client.csr
openssl x509 -req -days 3650 -in client.csr -CA ca.crt -CAkey ca.key -set_serial 01 -out client.crt
openssl pkcs12 -export -passout pass:qwerty -inkey client.key -in client.crt -out client.pfx
openssl pkcs12 -passin pass:qwerty -passout pass:qwerty -in client.pfx -out client.pem

rem Diffie–Hellman (D-H) key exchange
openssl dhparam -out dh4096.pem 4096
