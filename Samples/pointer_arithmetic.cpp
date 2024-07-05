extern "C" {
    char* char_pointer_plus_one(char* ptr)
    {
        return ptr + 1;
    }

    char* one_plus_char_pointer(char* ptr)
    {
        return 1 + ptr;
    }

    char* char_pointer_plus_value(char* ptr, int value)
    {
        return ptr + value;
    }

    char* value_plus_char_pointer(char* ptr, int value)
    {
        return value + ptr;
    }

    int* int_pointer_plus_one(int* ptr)
    {
        return ptr + 1;
    }

    int* one_plus_int_pointer(int* ptr)
    {
        return 1 + ptr;
    }

    int* int_pointer_plus_value(int* ptr, int value)
    {
        return ptr + value;
    }

    int* value_plus_int_pointer(int* ptr, int value)
    {
        return value + ptr;
    }
}