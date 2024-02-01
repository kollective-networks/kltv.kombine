
#include <stdio.h>
#include <lib.h>

int main(int argc, char** argv) {
	int a = 1;
	for(int i = 0; i < 10; i++) {
		a += i;
		printf("a = %d\n", a);
	}
	add(1,1);
	return 0;
}