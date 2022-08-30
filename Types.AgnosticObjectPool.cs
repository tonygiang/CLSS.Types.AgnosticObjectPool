// A part of the C# Language Syntactic Sugar suite.

using System;
using System.Collections.Generic;

namespace CLSS
{
  /// <summary>
  /// A domain-agnostic object pool. This class provides a minimal API that only
  /// handles pooling logic. Object creation logic and availability logic are
  /// left to you to customize.
  /// </summary>
  /// <typeparam name="T">The type of pooled objects. Must be a reference type.
  /// </typeparam>
  public class AgnosticObjectPool<T> where T : class
  {
    /// <summary>
    /// The number of pooled objects that will be created everytime the pool
    /// needs to grow.
    /// </summary>
    public int GrowNumberPerStep;

    /// <summary>
    /// The index of the backing list which will be checked for availability
    /// first the next time you take an object from the pool. This index
    /// automatically loops around.
    /// </summary>
    public int NextCheckingIndex = 0;

    /// <summary>
    /// The list of all pooled objects, including the unavailable objects.
    /// </summary>
    public List<T> Instances = new List<T>();

    /// <summary>
    /// The function that produces a new pooled object. This is assigned on
    /// construction. Although this can be replaced afterward, it should not
    /// be necessary in normal use cases to keep pooled objects consistent.
    /// </summary>
    public Func<T> ObjectFactory = null;

    /// <summary>
    /// The funtion that checks for object availability. This is assigned on
    /// construction. Although this can be replaced afterward, it should not
    /// be necessary in normal use cases to keep pooled objects consistent.
    /// </summary>
    public Func<T, bool> AvailableInstancePredicate = null;

    /// <summary>
    /// Initializes a new <see cref="AgnosticObjectPool{T}"/>.
    /// </summary>
    /// <param name="initialSize">The initial number of objects that will be
    /// created and added into the pool.</param>
    /// <param name="growNumberPerStep">The number of pooled objects that will
    /// be created everytime the pool needs to grow.</param>
    /// <param name="objectFactory">The function that produces a new pooled
    /// object.</param>
    /// <param name="availableInstancePredicate">The funtion that checks for
    /// object availability.</param>
    public AgnosticObjectPool(int initialSize,
      int growNumberPerStep,
      Func<T> objectFactory,
      Func<T, bool> availableInstancePredicate)
    {
      GrowNumberPerStep = growNumberPerStep;
      ObjectFactory = objectFactory;
      AvailableInstancePredicate = availableInstancePredicate;
      Instances.Capacity = initialSize;
      Grow(initialSize);
    }

    /// <summary>
    /// Grows the pool by the specified number of objects.
    /// </summary>
    /// <param name="number">The number of new objects to create.</param>
    /// <returns>The object pool.</returns>
    public virtual AgnosticObjectPool<T> Grow(int number)
    {
      for (int i = 0; i < number; ++i) Instances.Add(ObjectFactory());
      return this;
    }

    /// <summary>
    /// Grows the pool by the specified number of steps. Each step grows the
    /// pool by the number defined by <see cref="GrowNumberPerStep"/>
    /// </summary>
    /// <param name="number">The number of steps to grow the pool.</param>
    /// <returns>The object pool.</returns>
    public virtual AgnosticObjectPool<T> GrowStep(int stepNumber)
    { return Grow(stepNumber * GrowNumberPerStep); }

    /// <summary>
    /// Allocates and returns a new array filled with a specified number of
    /// available objects from the pool.
    /// <br/>
    /// This method can trigger a pool growth if there are not enough available
    /// pooled objects.
    /// </summary>
    /// <param name="number">The number of objects to take.</param>
    /// <returns>The newly-allocated array of available objects.</returns>
    public virtual T[] Take(int number) { return TakeAndFill(new T[number]); }

    /// <summary>
    /// Fills the specified buffer array with available objects from the pool.
    /// Returns that array.
    /// <br/>
    /// This method can trigger a pool growth if there are not enough available
    /// pooled objects.
    /// </summary>
    /// <param name="buffer"><inheritdoc cref="TakeAndFill(T[], int)"
    /// path="/param[@name='buffer']"/></param>
    /// <returns><inheritdoc cref="TakeAndFill(T[], int)"
    /// path="/returns"/></returns>
    public virtual T[] TakeAndFill(T[] buffer)
    { return TakeAndFill(buffer, buffer.Length); }

    /// <summary>
    /// Fills the specified buffer array with a specified number of available
    /// objects from the pool. The buffer array will be null-terminated, meaning
    /// elements after the specified number of objects to take will be null.
    /// Returns that array.
    /// <br/>
    /// This method can trigger a pool growth if there are not enough available
    /// pooled objects.
    /// </summary>
    /// <param name="buffer">The buffer array that will be filled.</param>
    /// <param name="number">The number of elements in the buffer array that
    /// will be assigned.</param>
    /// <returns>The filled buffer array.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="buffer"/> is
    /// null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="number"/>
    /// is not within the range of <paramref name="buffer"/>'s length.
    /// </exception>
    public virtual T[] TakeAndFill(T[] buffer, int number)
    {
      if (buffer == null) throw new ArgumentNullException("buffer");
      if (number > buffer.Length || number < 0)
        throw new ArgumentOutOfRangeException("number");
      for (int i = 0; i < buffer.Length; ++i) buffer[i] = null;
      int matchedCount = 0;
      for (int i = 0; i < Instances.Count; ++i)
      {
        T instance = Instances[NextCheckingIndex];
        NextCheckingIndex = (NextCheckingIndex + 1) % Instances.Count;
        if (AvailableInstancePredicate(instance))
        {
          buffer[matchedCount] = instance; ++matchedCount;
          if (matchedCount == number) return buffer;
        }
      }
      int unfilledNumber = number - matchedCount;
      int growStepNeeded = (unfilledNumber + GrowNumberPerStep - 1)
        / GrowNumberPerStep;
      GrowStep(growStepNeeded);
      NextCheckingIndex = Instances.Count - GrowNumberPerStep + unfilledNumber;
      for (int i = NextCheckingIndex - unfilledNumber;
        i < NextCheckingIndex;
        ++i)
      { buffer[matchedCount] = Instances[i]; ++matchedCount; }
      return buffer;
    }

    /// <summary>
    /// Returns the first found available object from the pool.
    /// <br/>
    /// This method can trigger a pool growth if there are not enough available
    /// pooled objects.
    /// </summary>
    /// <returns>An available object from the pool.</returns>
    public virtual T TakeOne()
    {
      for (int i = 0; i < Instances.Count; ++i)
      {
        T instance = Instances[NextCheckingIndex];
        NextCheckingIndex = (NextCheckingIndex + 1) % Instances.Count;
        if (AvailableInstancePredicate(instance)) return instance;
      }
      GrowStep(1);
      NextCheckingIndex = Instances.Count - GrowNumberPerStep + 1;
      return Instances[NextCheckingIndex - 1];
    }
  }
}