
#include <stdio.h>
#include <openssl/md5.h>

int main(int argc, char** argv) {

    unsigned char result[MD5_DIGEST_LENGTH];
    const unsigned char* str;
    str = (unsigned char*)"hello";
    unsigned int long_size = 100;
    MD5(str,long_size,result);
	return 0;
}