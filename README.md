# CLSS.Types.AgnosticObjectPool

### Problem

Pooling heap objects is a common practice in many domains that have class types that are mutable and highly interchangeable with each other. Unfortunately, object pooling solutions in .NET ecosystem are highly domain-specific even though at their core, there are pooling logics that remain the same. They could have been ported from projects to projects more easily if they don't contain domain-specific code.

### Solution

True to its name, `AgnosticObjectPool` is a domain-agnostic object pool data structure that only contains pooling logic but *adapts to any domain*.

`AgnosticObjectPool` works with a very minimal API. It does not determine whether an object is in or out of its pool by mutating its backing collection, nor does it keep track of each pooled objects' used or unused status via a hash table mapping. Instead, it only determines each pooled objects's "availability" status upon request using a user-defined `Func<T, bool>` delegate. However this pooled object becomes "unavailable" if passed through the same `Func<T, bool>` delegate again is highly domain-specific and left up to the user.

Classes in the API you are using that have fields/properties such as `IsOn`, `IsEnabled`, `IsVisible`, etc... are the most straightforward use cases for `AgnosticObjectPool`, as their availability-checking delegate can be defined as simply as `obj => !obj.IsOn`.

The decision to design `AgnosticObjectPool` this way came from the observation that many frameworks you may work with don't always provide the hook for developers to get a callback at the moment a class instance is effectively no longer in use (such as an audio object finishing playing a clip, or a visual effect object finishing playing an animation). `AgnosticObjectPool` was designed to adapt to your domain logic, not to do the impossible task of forcing domains to adapt to it.

At construction, it takes in the following arguments:

- `int initialSize`: The number of objects that will be immediately added to the pool upon construction.
- `int growNumberPerStep`: The number of pooled objects that will be created everytime the pool needs to grow. This lets you instruct the pool to grow in steps instead of growing one more everytime it runs out of objects that can be taken. This is assigned to the public field `GrowNumberPerStep`.
- `Func<T> objectFactory`: The function that defines how a new object will be created. `AgnosticObjectPool` does not assume that your domains always create new class instances by a `new` operator, therefore it requires object creation to be defined by you. This is assigned to the public field `ObjectFactory`.
- `Func<T, bool> availableInstancePredicate`: The function that checks pooled objects for availability. Objects are deemed "available" if the results of passing them to this function is true. ***However your domain's logic goes, you should make sure that this result is true upon creation then immediately becomes false after taking an available object from the pool and make sure it becomes true when that object is no longer in use***. Making sure of this does not necessarily mean doing something manually, the framework APIs you are working with may already be setting the status of some fields/properties for you. You only have to give `AgnosticObjectPool` the function instructing it to check those fields/properties. This is assigned to the public field `AvailableInstancePredicate`.

```csharp
using CLSS;

public class NetworkRequestHandler
{
  public bool Active = false;
  public void StartListen()
  {
    this.Active = true;
    // ...
  }
  public void Close()
  {
    this.Active = false;
    // ...
  }
}

var NRHPool = new AgnosticObjectPool<NetworkRequestHandler>(20,
  4,
  NetworkRequestHandler.Create,
  h => !h.Active);

// Take one from the pool and use
NRHPool.TakeOne().StartListen();
```

`TakeOne` is the method that will give you one available pooled object. In order to take more than 1, you have the options between an allocating method and a non-allocating method.

`Take` is the method that will allocate a new array for results and return it filled with available pooled objects.

```csharp
using CLSS;

var AudioPool = new AgnosticObjectPool<AudioPlayer>(5,
  5,
  () => new AudioPlayer(),
  ap => !ap.IsPlaying);
AudioPlayer[] apToUse = AudioPool.Take(3);
```

`TakeAndFill` is the method that does not allocate a new array for results by itself. It instead takes in a pre-allocated buffer array and fills that.

```csharp
using CLSS;

var apToUse = new AudioPlayer[3];
AudioPool.TakeAndFill(apToUse); // apToUse is filled with results after this line;
```

`TakeAndFill` additionally takes in an optional number of objects to fill. If this number is smaller than the buffer array's length, the buffer array will be null-terminated. That means if you call `TakeAndFill(buffer, n)`, only the first `n` elements of the buffer will contain available objects from the pool and the rest are `null`.

```csharp
using CLSS;

public static AudioPlayer[] AudioPlayerBuffer = new AudioPlayer[256];

AudioPool.TakeAndFill(AudioPlayerBuffer, 8);
// Only the first 8 elements are results, 9th element and afterward are null.
for (int i = 0; i < 8; ++i) { var ap = AudioPlayerBuffer[i]; }
```

`TakeOne`, `Take` and `TakeAndFill` will trigger a growth upon finding that all objects in the pool are unavailable. `AgnosticObjectPool` grows in steps, and in the case of `Take` and `TakeAndFill`, it will grow the minimum number of steps as required.

#### Advanced Usage Notes

`TakeOne` method has a complexity of O(1) in the best-case scenario and O(n) in the worst case scenario. In order to hit the best-case scenario as frequently as possible. It keeps track of the next index to check via a field named `NextCheckingIndex`. This field is automatically set by `AgnosticObjectPool`. It increments everytime you take an available object from the pool and automatically loops around when it reaches the end of the pool. `NextCheckingIndex` is made public should you need to manipulate it directly, even though it is not recommended to do so.

You may want to manually and pre-emptively grow the object pool to cut down on heavy heap allocations during hot code paths by calling the `Grow` and `GrowStep` methods. `Grow` takes in the total number of objects to create and `GrowStep` takes in the number of steps to grow. The number of objects that will be created is defined by the public field `GrowNumberPerStep`.

The list of `AgnosticObjectPool`'s instances is also publicly exposed via the field `Instances`. You can trim down the pool by manipulating this list directly, but be mindful of `NextCheckingIndex` if you do this. It may have an out-of-range value the next time you take an object after trimming down the pool.

##### This package is a part of the [C# Language Syntactic Sugar suite](https://github.com/tonygiang/CLSS).