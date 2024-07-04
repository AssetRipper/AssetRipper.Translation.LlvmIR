extern "C" {
    int sum(int* values, int count)
    {
        int sum = 0;
        for (int i = 0; i < count; i++)
        {
            sum += values[i];
        }
        return sum;
    }
}