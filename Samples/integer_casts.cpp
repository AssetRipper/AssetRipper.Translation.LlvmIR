extern "C" {
    long long i32_to_i64(int value)
    {
        return value;
    }

    unsigned long long u32_to_u64(unsigned int value)
    {
        return value;
    }

    long long u32_to_i64(unsigned int value)
    {
        return value;
    }

    bool i32_to_i1(int value)
    {
        return value;
    }

    int i64_to_i32(long long value)
    {
        return (int)value;
    }
}