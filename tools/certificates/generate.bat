# Certificate Authority (CA)
openssl.exe genrsa -des3 -passout pass:qwerty -out ca-secret.key 4096
openssl.exe rsa -passin pass:qwerty -in ca-secret.key -out ca.key
openssl.exe req -new -x509 -days 3650 -subj "/C=BY/ST=Belarus/L=Minsk/O=Example root CA/OU=Example CA unit/CN=example.com" -key ca.key -out ca.crt -config openssl.cfg
openssl.exe pkcs12 -clcerts -export -passout pass:qwerty -in ca.crt -inkey ca.key -out ca.p12
openssl.exe pkcs12 -clcerts -passin pass:qwerty -passout pass:qwerty -in ca.p12 -out ca.pem

# SSL Server certificate
openssl.exe genrsa -des3 -passout pass:qwerty -out server-secret.key 4096
openssl.exe rsa -passin pass:qwerty -in server-secret.key -out server.key
openssl.exe req -new -subj "/C=BY/ST=Belarus/L=Minsk/O=Example server/OU=Example server unit/CN=server.example.com" -key server.key -out server.csr -config openssl.cfg
openssl.exe x509 -req -days 3650 -in server.csr -CA ca.crt -CAkey ca.key -set_serial 01 -out server.crt
openssl.exe pkcs12 -clcerts -export -passout pass:qwerty -in server.crt -inkey server.key -out server.p12
openssl.exe pkcs12 -clcerts -passin pass:qwerty -passout pass:qwerty -in server.p12 -out server.pem

# SSL Client certificate
openssl.exe genrsa -des3 -passout pass:qwerty -out client-secret.key 4096
openssl.exe rsa -passin pass:qwerty -in client-secret.key -out client.key
openssl.exe req -new -subj "/C=BY/ST=Belarus/L=Minsk/O=Example client/OU=Example client unit/CN=client.example.com" -key client.key -out client.csr -config openssl.cfg
openssl.exe x509 -req -days 3650 -in client.csr -CA ca.crt -CAkey ca.key -set_serial 01 -out client.crt
openssl.exe pkcs12 -clcerts -export -passout pass:qwerty -in client.crt -inkey client.key -out client.p12
openssl.exe pkcs12 -clcerts -passin pass:qwerty -passout pass:qwerty -in client.p12 -out client.pem

# Diffie–Hellman (D-H) key exchange
openssl.exe dhparam -out dh4096.pem 4096
