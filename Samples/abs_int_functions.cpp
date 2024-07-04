extern "C" {
    int abs_if_blocks(int value)
    {
        if (value < 0)
        {
            return -value;
        }
        else
        {
            return value;
        }
    }

    int abs_condition_expression(int value)
    {
        return value < 0 ? -value : value;
    }
}