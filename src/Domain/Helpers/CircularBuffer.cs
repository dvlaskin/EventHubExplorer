using System.Collections;

namespace Domain.Helpers;

public class CircularBuffer<T> : IEnumerable<T>
{
    private readonly T[] buffer;
    private readonly int maxSize;
    private int head = 0;
    private int tail = 0;
    private int count = 0;
    private int totalAddedCount = 0;
    private readonly Lock lockObj = new();

    public CircularBuffer(int maxSize)
    {
        if (maxSize <= 0)
            throw new ArgumentException("The size of the buffer must be greater than 0", nameof(maxSize));
            
        this.maxSize = maxSize;
        buffer = new T[maxSize];
    }

    public int Count 
    { 
        get 
        { 
            lock (lockObj) 
            { 
                return count; 
            } 
        } 
    }

    public int TotalAddedCount
    {
        get
        {
            lock (lockObj)
            {
                return totalAddedCount;
            }
        }
    }
    
    public int Capacity => maxSize;

    public bool IsFull 
    { 
        get 
        { 
            lock (lockObj) 
            { 
                return count == maxSize; 
            } 
        } 
    }

    /// <summary>
    /// Add element to buffer. If buffer is full, delete the oldest element 
    /// </summary>
    public void Add(T item)
    {
        lock (lockObj)
        {
            buffer[tail] = item;
            tail = (tail + 1) % maxSize;

            if (count < maxSize)
            {
                count++;
            }
            else
            {
                // buffer is full, move head (delete the oldest element)
                head = (head + 1) % maxSize;
            }
            
            totalAddedCount++;
        }
    }

    /// <summary>
    /// Get element by index (0 = the oldest element)
    /// </summary>
    public T this[int index]
    {
        get
        {
            lock (lockObj)
            {
                if (index < 0 || index >= count)
                    throw new IndexOutOfRangeException();

                return buffer[(head + index) % maxSize];
            }
        }
    }

    /// <summary>
    /// Calculate virtual index of the element,
    /// virtual index is the index of the element in the buffer if it was not circular
    /// </summary>
    /// <param name="index"></param>
    /// <returns></returns>
    public int GetVirtualIndex(int index)
    {
        if (totalAddedCount <= maxSize)
            return index;
        
        return totalAddedCount - (maxSize - index);
    }
    
    /// <summary>
    /// Get the latest element
    /// </summary>
    public bool TryGetLast(out T? res)
    {
        lock (lockObj)
        {
            if (count == 0)
            {
                res = default;
                return false;
            }

            int lastIndex = (tail - 1 + maxSize) % maxSize;
            res = buffer[lastIndex];
            return true;
        }
    }

    /// <summary>
    /// Get first (the oldest) element
    /// </summary>
    public bool TryGetFirst(out T? res)
    {
        lock (lockObj)
        {
            if (count == 0)
            {
                res = default;
                return false;
            }

            res = buffer[head];
            return true;
        }
    }

    /// <summary>
    /// Clean buffer
    /// </summary>
    public void Clear()
    {
        lock (lockObj)
        {
            Array.Clear(buffer, 0, buffer.Length);
            head = 0;
            tail = 0;
            count = 0;
            totalAddedCount = 0;
        }
    }

    /// <summary>
    /// Get all elements as array (from oldest to latest)
    /// </summary>
    public T[] ToArray()
    {
        lock (lockObj)
        {
            if (count == 0)
                return new T[0];

            T[] result = new T[count];
            for (int i = 0; i < count; i++)
            {
                result[i] = buffer[(head + i) % maxSize];
            }
            return result;
        }
    }

    /// <summary>
    /// Get all elements as list (from older to latest) 
    /// </summary>
    public List<T> ToList()
    {
        return ToArray().ToList();
    }

    public IEnumerator<T> GetEnumerator()
    {
        // Create a snapshot for safe iteration
        T[] snapshot = ToArray();
        return ((IEnumerable<T>)snapshot).GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}