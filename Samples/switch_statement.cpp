extern "C" {
    int fake_fibonacci(int value)
    {
        switch (value)
        {
            case 0:
                return 0;
            case 1:
                return 1;
            case 2:
                return 1;
            case 3:
                return 2;
            case 4:
                return 3;
            case 5:
                return 5;
            case 6:
                return 8;
            default:
                return 0;
        }
    }

    int switch_with_holes(int value)
    {
        switch (value)
        {
            case 0:
                return 0;
            case 2:
                return 1;
            case 3:
                return 2;
            case 4:
                return 3;
            case 6:
                return 5;
            case 7:
                return 8;
            case 8:
                return 9;
            case 10:
                return 10;
            case 11:
                return 11;
            default:
                return 0;
        }
    }
}