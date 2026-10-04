var arr = new int[1000];

for (var i = 0; i < 1000; i++)
{
    arr[i] = i;
}

arr.ToList().ForEach(Console.WriteLine);