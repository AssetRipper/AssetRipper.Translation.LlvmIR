extern "C" {
    void incrementRef(int* value)
    {
        (*value)++;
    }

    void decrementRef(int* value)
    {
        (*value)--;
    }
}