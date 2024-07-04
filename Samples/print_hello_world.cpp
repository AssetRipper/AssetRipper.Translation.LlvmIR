#include <iostream>

extern "C" {
    void print_hello_world()
    {
        std::cout << "Hello world!" << std::endl;
    }
}