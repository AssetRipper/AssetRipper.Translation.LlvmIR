extern "C" {
    void incrementRef(int* value)
    {
        (*value)++;
    }

    void decrementRef(int* value)
    {
        (*value)--;
    }

    int accessRef(int* value)
    {
        return *value;
    }

    void* accessSuperRef(void** pointerToPointer)
    {
        return *pointerToPointer;
    }

    void* accessSuperSuperRef(void*** arg)
    {
        return *(*arg);
    }

    void* returnSuperRef(void** pointerToPointer)
    {
        return pointerToPointer;
    }
}