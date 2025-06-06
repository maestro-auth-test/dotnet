﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.Diagnostics.DebugServices;
using Microsoft.Diagnostics.Runtime;
using Microsoft.Diagnostics.Runtime.Interfaces;

namespace Microsoft.Diagnostics.ExtensionCommands
{
    public class ClrMDHelper
    {
        private readonly ClrRuntime _clr;
        private readonly ClrHeap _heap;

        [ServiceExport(Scope = ServiceScope.Runtime)]
        public static ClrMDHelper TryCreate([ServiceImport(Optional = true)] ClrRuntime clrRuntime)
        {
            try
            {
                if (clrRuntime != null)
                {
                    return new ClrMDHelper(clrRuntime);
                }
            }
            catch (NotSupportedException ex)
            {
                Trace.TraceError(ex.ToString());
            }
            return null;
        }

        private ClrMDHelper(ClrRuntime clr)
        {
            Debug.Assert(clr != null);
            _clr = clr;
            _heap = _clr.Heap;
        }

        public ulong GetTaskStateFromAddress(ulong address)
        {
            const string stateFieldName = "m_stateFlags";

            ClrType type = _heap.GetObjectType(address);
            if ((type != null) && (type.Name.StartsWith("System.Threading.Tasks.Task")))
            {
                // could be other Task-prefixed types in the same namespace such as TaskCompletionSource
                if (type.GetFieldByName(stateFieldName) == null)
                {
                    return 0;
                }

                return (ulong)_heap.GetObject(address).ReadField<int>(stateFieldName);
            }

            return 0;
        }

        public static string GetTaskState(ulong flag)
        {
            TaskStatus tks;

            if ((flag & TASK_STATE_FAULTED) != 0)
            {
                tks = TaskStatus.Faulted;
            }
            else if ((flag & TASK_STATE_CANCELED) != 0)
            {
                tks = TaskStatus.Canceled;
            }
            else if ((flag & TASK_STATE_RAN_TO_COMPLETION) != 0)
            {
                tks = TaskStatus.RanToCompletion;
            }
            else if ((flag & TASK_STATE_WAITING_ON_CHILDREN) != 0)
            {
                tks = TaskStatus.WaitingForChildrenToComplete;
            }
            else if ((flag & TASK_STATE_DELEGATE_INVOKED) != 0)
            {
                tks = TaskStatus.Running;
            }
            else if ((flag & TASK_STATE_STARTED) != 0)
            {
                tks = TaskStatus.WaitingToRun;
            }
            else if ((flag & TASK_STATE_WAITINGFORACTIVATION) != 0)
            {
                tks = TaskStatus.WaitingForActivation;
            }
            else if (flag == 0)
            {
                tks = TaskStatus.Created;
            }
            else
            {
                return null;
            }

            return tks.ToString();
        }

        // from CLR implementation in https://github.com/dotnet/runtime/blob/main/src/libraries/System.Private.CoreLib/src/System/Threading/Tasks/Task.cs#L141
        internal const int TASK_STATE_STARTED = 0x00010000;
        internal const int TASK_STATE_DELEGATE_INVOKED = 0x00020000;
        internal const int TASK_STATE_DISPOSED = 0x00040000;
        internal const int TASK_STATE_EXCEPTIONOBSERVEDBYPARENT = 0x00080000;
        internal const int TASK_STATE_CANCELLATIONACKNOWLEDGED = 0x00100000;
        internal const int TASK_STATE_FAULTED = 0x00200000;
        internal const int TASK_STATE_CANCELED = 0x00400000;
        internal const int TASK_STATE_WAITING_ON_CHILDREN = 0x00800000;
        internal const int TASK_STATE_RAN_TO_COMPLETION = 0x01000000;
        internal const int TASK_STATE_WAITINGFORACTIVATION = 0x02000000;
        internal const int TASK_STATE_COMPLETION_RESERVED = 0x04000000;
        internal const int TASK_STATE_WAIT_COMPLETION_NOTIFICATION = 0x10000000;
        internal const int TASK_STATE_EXECUTIONCONTEXT_IS_NULL = 0x20000000;
        internal const int TASK_STATE_TASKSCHEDULED_WAS_FIRED = 0x40000000;

        public IEnumerable<TimerInfo> EnumerateTimers()
        {
            // the implementation is different between .NET Framework/.NET Core 2.*, and .NET Core 3.0+
            // - the former is relying on a single static TimerQueue.s_queue
            // - the latter uses an array of TimerQueue (static TimerQueue.Instances field)
            // each queue refers to TimerQueueTimer linked list via its m_timers or _shortTimers/_longTimers fields
            ClrType timerQueueType = GetMscorlib().GetTypeByName("System.Threading.TimerQueue");
            if (timerQueueType == null)
            {
                yield break;
            }

            // .NET Core case
            ClrStaticField instancesField = timerQueueType.GetStaticFieldByName("<Instances>k__BackingField");
            if (instancesField != null)
            {
                // until the ClrMD bug to get static field value is fixed, iterate on each object of the heap
                // to find each TimerQueue and iterate on (slower but it works)
                foreach (ClrObject obj in _heap.EnumerateObjects())
                {
                    ClrType objType = obj.Type;
                    if (objType == null)
                    {
                        continue;
                    }

                    if (objType == _heap.FreeType)
                    {
                        continue;
                    }

                    if (objType.Name != "System.Threading.TimerQueue")
                    {
                        continue;
                    }

                    // m_timers is the start of the linked list of TimerQueueTimer in pre 3.0
                    ClrInstanceField timersField = objType.GetFieldByName("m_timers");
                    if (timersField != null)
                    {
                        ClrObject currentTimerQueueTimer = obj.ReadObjectField("m_timers");
                        foreach (TimerInfo timer in GetTimers(currentTimerQueueTimer, false))
                        {
                            yield return timer;
                        }
                    }
                    else
                    {
                        // get short timers
                        timersField = objType.GetFieldByName("_shortTimers");
                        if (timersField == null)
                        {
                            throw new InvalidOperationException("Missing _shortTimers field. Check the .NET Core version implementation of TimerQueue.");
                        }

                        ClrObject currentTimerQueueTimer = obj.ReadObjectField("_shortTimers");
                        foreach (TimerInfo timer in GetTimers(currentTimerQueueTimer, true))
                        {
                            timer.IsShort = true;
                            yield return timer;
                        }

                        // get long timers
                        currentTimerQueueTimer = obj.ReadObjectField("_longTimers");
                        foreach (TimerInfo timer in GetTimers(currentTimerQueueTimer, true))
                        {
                            timer.IsShort = false;
                            yield return timer;
                        }
                    }
                }
            }
            else
            {
                // .NET Framework implementation
                ClrStaticField instanceField = timerQueueType.GetStaticFieldByName("s_queue");
                if (instanceField == null)
                {
                    yield break;
                }

                foreach (ClrAppDomain domain in _clr.AppDomains)
                {
                    ClrObject timerQueue = instanceField.ReadObject(domain);
                    if ((timerQueue.IsNull) || (!timerQueue.IsValid))
                    {
                        continue;
                    }

                    // m_timers is the start of the list of TimerQueueTimer
                    ClrObject currentTimerQueueTimer = timerQueue.ReadObjectField("m_timers");
                    foreach (TimerInfo timer in GetTimers(currentTimerQueueTimer, false))
                    {
                        yield return timer;
                    }
                }
            }
        }

        private IEnumerable<TimerInfo> GetTimers(ClrObject timerQueueTimer, bool is30Format)
        {
            while (timerQueueTimer.Address != 0)
            {
                TimerInfo ti = GetTimerInfo(timerQueueTimer);
                if (ti == null)
                {
                    continue;
                }

                yield return ti;

                timerQueueTimer = is30Format ?
                    timerQueueTimer.ReadObjectField("_next") :
                    timerQueueTimer.ReadObjectField("m_next");
            }
        }

        private TimerInfo GetTimerInfo(ClrObject currentTimerQueueTimer)
        {
            TimerInfo ti = new()
            {
                TimerQueueTimerAddress = currentTimerQueueTimer.Address
            };

            // field names prefix changes from "m_" to "_" in .NET Core 3.0
            bool is30Format = currentTimerQueueTimer.Type.GetFieldByName("_dueTime") != null;
            ClrObject state;
            if (is30Format)
            {
                ti.DueTime = currentTimerQueueTimer.ReadField<uint>("_dueTime");
                ti.Period = currentTimerQueueTimer.ReadField<uint>("_period");
                ti.Cancelled = currentTimerQueueTimer.ReadField<bool>("_canceled");
                state = currentTimerQueueTimer.ReadObjectField("_state");
            }
            else
            {
                ti.DueTime = currentTimerQueueTimer.ReadField<uint>("m_dueTime");
                ti.Period = currentTimerQueueTimer.ReadField<uint>("m_period");
                ti.Cancelled = currentTimerQueueTimer.ReadField<bool>("m_canceled");
                state = currentTimerQueueTimer.ReadObjectField("m_state");
            }

            ti.StateAddress = 0;
            if (state.IsValid)
            {
                ti.StateAddress = state.Address;
                ClrType stateType = _heap.GetObjectType(ti.StateAddress);
                if (stateType != null)
                {
                    ti.StateTypeName = stateType.Name;
                }
            }

            // decipher the callback details
            ClrObject timerCallback = is30Format ?
                currentTimerQueueTimer.ReadObjectField("_timerCallback") :
                currentTimerQueueTimer.ReadObjectField("m_timerCallback");
            if (timerCallback.IsValid)
            {
                ClrType elementType = timerCallback.Type;
                if (elementType != null)
                {
                    if (elementType.Name == "System.Threading.TimerCallback")
                    {
                        ti.MethodName = BuildTimerCallbackMethodName(timerCallback);
                    }
                    else
                    {
                        ti.MethodName = "<" + elementType.Name + ">";
                    }
                }
                else
                {
                    ti.MethodName = "{no callback type?}";
                }
            }
            else
            {
                ti.MethodName = "???";
            }


            return ti;
        }

        private string BuildTimerCallbackMethodName(ClrObject timerCallback)
        {
            ulong methodPtr = timerCallback.ReadField<ulong>("_methodPtr");
            if (methodPtr != 0)
            {
                ClrMethod method = _clr.GetMethodByInstructionPointer(methodPtr);
                if (method != null)
                {
                    // look for "this" to figure out the real callback implementor type
                    string thisTypeName = "?";
                    ClrObject thisPtr = timerCallback.ReadObjectField("_target");
                    if (thisPtr.IsValid)
                    {
                        ulong thisRef = thisPtr.Address;
                        ClrType thisType = _heap.GetObjectType(thisRef);
                        if (thisType != null)
                        {
                            thisTypeName = thisType.Name;
                        }
                    }
                    else
                    {
                        thisTypeName = (method.Type != null) ? method.Type.Name : "?";
                    }
                    return $"{thisTypeName}.{method.Name}";
                }
                else
                {
                    return "";
                }
            }
            else
            {
                return "";
            }
        }

        public IEnumerable<ThreadPoolItem> EnumerateGlobalThreadPoolItems()
        {
            ClrModule mscorlib = GetMscorlib();
            return IsNetCore() ?
                EnumerateGlobalThreadPoolItemsInNetCore() :
                EnumerateGlobalThreadPoolItemsInNetFramework(mscorlib);
        }

        public IEnumerable<ThreadPoolItem> EnumerateLocalThreadPoolItems()
        {
            ClrModule mscorlib = GetMscorlib();
            return IsNetCore() ?
                EnumerateLocalThreadPoolItemsInNetCore() :
                EnumerateLocalThreadPoolItemsInNetFramework(mscorlib);
        }

        private IEnumerable<ThreadPoolItem> EnumerateGlobalThreadPoolItemsInNetCore()
        {
            // Look at the code to enumerate ThreadPool items from ThreadPool.cs
            // in https://github.com/dotnet/runtime/blob/ee9dc4984ce172697d94471a6be57d61116e34b6/src/libraries/System.Private.CoreLib/src/System/Threading/ThreadPool.cs#L1184
            //
            // Global queue is stored in ThreadPoolGlobals.workQueue (a ThreadPoolWorkQueue)
            // and each thread has a dedicated WorkStealingQueue stored in WorkStealingQueueList._queues (a WorkStealingQueue[])
            //
            // until we can access static fields values in .NET Core with ClrMD, we need to browse the whole heap...
            ClrHeap heap = _clr.Heap;
            if (!heap.CanWalkHeap)
            {
                yield break;
            }

            foreach (ClrObject obj in _heap.EnumerateObjects())
            {

                ClrType objType = obj.Type;
                if (objType == null)
                {
                    continue;
                }

                if (objType == _heap.FreeType)
                {
                    continue;
                }

                if (objType.Name == "System.Threading.ThreadPoolWorkQueue")
                {
                    // work items are stored in a ConcurrentQueue stored in the "workItems" field
                    ClrInstanceField workItemsField = objType.GetFieldByName("workItems");
                    if (workItemsField == null)
                    {
                        throw new InvalidOperationException("Unsupported version of .NET: missing 'workItems' field in ThreadPoolWorkQueue");
                    }

                    ClrObject workItems = obj.ReadObjectField("workItems");

                    foreach (ClrObject workItem in EnumerateWorkItemsAddressInConcurrentQueue(workItems.Address))
                    {
                        yield return GetThreadPoolItem(workItem);
                    }
                    break;
                }
            }
        }
        private ThreadPoolItem GetThreadPoolItem(ClrObject item)
        {
            ClrType itemType = item.Type;
            if (itemType.Name == "System.Threading.Tasks.Task")
            {
                return GetTask(item);
            }

            if (itemType.Name is
                "System.Threading.QueueUserWorkItemCallback" or
                "System.Threading.QueueUserWorkItemCallbackDefaultContext") //new to .net core
            {
                return GetQueueUserWorkItemCallback(item);
            }

            // create a raw information
            ThreadPoolItem tpi = new()
            {
                Type = ThreadRoot.Raw,
                Address = item.Address,
                MethodName = itemType.Name
            };

            return tpi;
        }

        private ThreadPoolItem GetTask(ClrObject task)
        {
            ThreadPoolItem tpi = new()
            {
                Address = task.Address,
                Type = ThreadRoot.Task
            };

            // look for the context in m_action._target
            ClrObject action = task.ReadObjectField("m_action");
            if (!action.IsValid)
            {
                tpi.MethodName = " [no action]";
                return tpi;
            }

            ClrObject target = action.ReadObjectField("_target");
            if (!target.IsValid)
            {
                tpi.MethodName = " [no target]";
                return tpi;
            }

            tpi.MethodName = BuildDelegateMethodName(target.Type, action);

            // get the task scheduler if any
            ClrObject taskScheduler = task.ReadObjectField("m_taskScheduler");
            if (taskScheduler.IsValid)
            {
                string schedulerType = taskScheduler.Type.ToString();
                if ("System.Threading.Tasks.ThreadPoolTaskScheduler" != schedulerType)
                {
                    tpi.MethodName = $"{tpi.MethodName} [{schedulerType}]";
                }
            }
            return tpi;
        }

        private ThreadPoolItem GetQueueUserWorkItemCallback(ClrObject element)
        {
            ThreadPoolItem tpi = new()
            {
                Address = (ulong)element,
                Type = ThreadRoot.WorkItem
            };

            // look for the callback given to ThreadPool.QueueUserWorkItem()
            // for .NET Core, the callback is stored in a different field _callback
            ClrType elementType = element.Type;
            ClrObject callback;
            if (elementType.GetFieldByName("_callback") != null)
            {
                callback = element.ReadObjectField("_callback");
            }
            else if (elementType.GetFieldByName("callback") != null)
            {
                callback = element.ReadObjectField("callback");
            }
            else
            {
                tpi.MethodName = "[no callback]";
                return tpi;
            }

            ClrObject target = callback.ReadObjectField("_target");
            if (!target.IsValid)
            {
                tpi.MethodName = "[no callback target]";
                return tpi;
            }

            ClrType targetType = target.Type;
            if (targetType == null)
            {
                tpi.MethodName = $" [target=0x{target.Address}]";
            }
            else
            {
                // look for method name
                tpi.MethodName = BuildDelegateMethodName(targetType, callback);
            }

            return tpi;
        }

        internal string BuildDelegateMethodName(ClrType targetType, ClrObject action)
        {
            ulong methodPtr = action.ReadField<ulong>("_methodPtr");
            if (methodPtr != 0)
            {
                ClrMethod method = _clr.GetMethodByInstructionPointer(methodPtr);
                if (method == null)
                {
                    // could happen in case of static method
                    methodPtr = action.ReadField<ulong>("_methodPtrAux");
                    method = _clr.GetMethodByInstructionPointer(methodPtr);
                }

                if (method != null)
                {
                    // anonymous method
                    if (method.Type.Name == targetType.Name)
                    {
                        return $"{targetType.Name}.{method.Name}";
                    }
                    else
                    // method is implemented by an class inherited from targetType
                    // ... or a simple delegate indirection to a static/instance method
                    {
                        if (targetType.Name == "System.Threading.WaitCallback"
                            || targetType.Name.StartsWith("System.Action<", StringComparison.Ordinal))
                        {
                            return $"{method.Type.Name}.{method.Name}";
                        }
                        else
                        {
                            return $"({targetType.Name}){method.Type.Name}.{method.Name}";
                        }
                    }
                }
            }

            return "";
        }

        private IEnumerable<ClrObject> EnumerateWorkItemsAddressInConcurrentQueue(ulong address)
        {
            // Look at EnumerateConcurrentQueue() for more details about ConcurrentQueue
            ClrObject cq = _heap.GetObject(address);
            ClrObject currentSegment = cq.ReadObjectField("_head");
            while (!currentSegment.IsNull && currentSegment.IsValid)
            {
                ClrArray slots = currentSegment.ReadObjectField("_slots").AsArray();
                int count = slots.Length;
                for (int current = 0; current < count; current++)
                {
                    ClrValueType slot = slots.GetStructValue(current);
                    UIntPtr slotEntry = slot.ReadField<UIntPtr>("Item");

                    // skip empty null slots
                    if (slotEntry == UIntPtr.Zero)
                    {
                        continue;
                    }

                    yield return _heap.GetObject(slotEntry.ToUInt64());
                }

                currentSegment = currentSegment.ReadObjectField("_nextSegment");
            };
        }

        private IEnumerable<ThreadPoolItem> EnumerateLocalThreadPoolItemsInNetCore()
        {
            // in .NET Core, each thread has a dedicated WorkStealingQueue stored in WorkStealingQueueList._queues (a WorkStealingQueue[])
            //
            // until we can access static fields values in .NET Core with ClrMD, we need to browse the whole heap...
            ClrHeap heap = _clr.Heap;
            if (!heap.CanWalkHeap)
            {
                yield break;
            }

            foreach (ClrObject obj in heap.EnumerateObjects())
            {
                if (!obj.IsValid)
                {
                    continue;
                }

                ClrType type = obj.Type;
                if (type == null)
                {
                    continue;
                }

                if (type.Name == "System.Threading.ThreadPoolWorkQueue+WorkStealingQueue")
                {
                    ClrObject stealingQueue = obj;
                    ClrArray workItems = stealingQueue.ReadObjectField("m_array").AsArray();
                    for (int current = 0; current < workItems.Length; current++)
                    {
                        ClrObject workItem = workItems.GetObjectValue(current);
                        if (!workItem.IsValid)
                        {
                            continue;
                        }

                        yield return GetThreadPoolItem(workItem);
                    }
                }
            }
        }

        // The ThreadPool is keeping track of the pending work items into two different areas:
        // - a global queue: stored by ThreadPoolWorkQueue instances of the ThreadPoolGlobals.workQueue static field
        // - several per thread (TLS) local queues: stored in SparseArray<ThreadPoolWorkQueue+WorkStealingQueue> linked from ThreadPoolWorkQueue.allThreadQueues static fields
        // both are using arrays of Task or QueueUserWorkItemCallback
        //
        // NOTE: don't show other thread pool related topics such as timer callbacks or wait objects
        //
        private IEnumerable<ThreadPoolItem> EnumerateGlobalThreadPoolItemsInNetFramework(ClrModule mscorlib)
        {

            ClrType queueType = mscorlib.GetTypeByName("System.Threading.ThreadPoolGlobals");
            if (queueType == null)
            {
                yield break;
            }

            ClrStaticField workQueueField = queueType.GetStaticFieldByName("workQueue");
            if (workQueueField == null)
            {
                yield break;
            }

            // the CLR keeps one static instance per application domain
            foreach (ClrAppDomain appDomain in _clr.AppDomains)
            {
                ClrObject workQueue = workQueueField.ReadObject(appDomain);
                if (!workQueue.IsValid)
                {
                    continue;
                }

                // should be  System.Threading.ThreadPoolWorkQueue
                ClrType workQueueType = workQueue.Type;
                if (workQueueType == null)
                {
                    continue;
                }

                if (workQueueType.Name != "System.Threading.ThreadPoolWorkQueue")
                {
                    continue;
                }

                foreach (ThreadPoolItem item in EnumerateThreadPoolWorkQueue(workQueue))
                {
                    yield return item;
                }
            }
        }

        private IEnumerable<ThreadPoolItem> EnumerateThreadPoolWorkQueue(ClrObject workQueue)
        {
            // start from the tail and follow the Next
            ClrObject currentQueueSegment = workQueue.ReadObjectField("queueTail");
            while (currentQueueSegment.IsValid)
            {
                // get the System.Threading.ThreadPoolWorkQueue+QueueSegment nodes array
                ClrArray nodes = currentQueueSegment.ReadObjectField("nodes").AsArray();
                for (int currentNode = 0; currentNode < nodes.Length; currentNode++)
                {
                    ClrObject item = nodes.GetObjectValue(currentNode);
                    if (!item.IsValid)
                    {
                        continue;
                    }

                    yield return GetThreadPoolItem(item);
                }

                currentQueueSegment = currentQueueSegment.ReadObjectField("Next");
            }
        }

        private IEnumerable<ThreadPoolItem> EnumerateLocalThreadPoolItemsInNetFramework(ClrModule mscorlib)
        {
            // look into the local stealing queues in each thread TLS
            // hopefully, they are all stored in static (one per app domain) instance
            // of ThreadPoolWorkQueue.SparseArray<ThreadPoolWorkQueue.WorkStealingQueue>
            //
            ClrType queueType = mscorlib.GetTypeByName("System.Threading.ThreadPoolWorkQueue");
            if (queueType == null)
            {
                yield break;
            }

            ClrStaticField threadQueuesField = queueType.GetStaticFieldByName("allThreadQueues");
            if (threadQueuesField == null)
            {
                yield break;
            }

            foreach (ClrAppDomain domain in _clr.AppDomains)
            {
                ClrObject threadQueue = threadQueuesField.ReadObject(domain);
                if (!threadQueue.IsValid)
                {
                    continue;
                }

                ClrArray sparseArray = threadQueue.ReadObjectField("m_array").AsArray();
                for (int current = 0; current < sparseArray.Length; current++)
                {
                    ClrObject stealingQueue = sparseArray.GetObjectValue(current);
                    if (!stealingQueue.IsValid)
                    {
                        continue;
                    }

                    foreach (ThreadPoolItem item in EnumerateThreadPoolStealingQueue(stealingQueue))
                    {
                        yield return item;
                    }
                }
            }
        }

        private IEnumerable<ThreadPoolItem> EnumerateThreadPoolStealingQueue(ClrObject stealingQueue)
        {
            ClrArray array = stealingQueue.ReadObjectField("m_array").AsArray();
            for (int current = 0; current < array.Length; current++)
            {
                ClrObject item = array.GetObjectValue(current);
                if (!item.IsValid)
                {
                    continue;
                }

                yield return GetThreadPoolItem(item);
            }
        }

        public IEnumerable<KeyValuePair<string, string>> EnumerateConcurrentDictionary(ulong address)
        {
            bool isNetCore = IsNetCore();
            string tablesFieldName = isNetCore ? "_tables" : "m_tables";
            string bucketsFieldName = isNetCore ? "_buckets" : "m_buckets";
            string keyFieldName = isNetCore ? "_key" : "m_key";
            string valueFieldName = isNetCore ? "_value" : "m_value";
            string nextFieldName = isNetCore ? "_next" : "m_next";

            ClrObject cd = _heap.GetObject(address);
            if (!cd.IsValid)
            {
                throw new InvalidOperationException("Address does not correspond to a ConcurrentDictionary");
            }

            ClrObject tables = cd.ReadObjectField(tablesFieldName);
            if (!tables.IsValid)
            {
                throw new InvalidOperationException($"ConcurrentDictionary does not own {tablesFieldName} attribute");
            }

            ClrObject buckets = tables.ReadObjectField(bucketsFieldName);
            if (!buckets.IsValid)
            {
                throw new InvalidOperationException($"ConcurrentDictionary tables does not own {bucketsFieldName} attribute");
            }

            ClrArray bucketsArray = buckets.AsArray();

            for (int i = 0; i < bucketsArray.Length; i++)
            {
                ClrObject node;
                // This field is only in .NET 8 and above. Currently can't use "_clr.ClrInfo.Version.Major >= 8" because
                // this version isn't the runtime version for single-file apps.
                if (cd.Type?.GetFieldByName("_comparerIsDefaultForClasses") != null)
                {
                    // On .NET 8 or more, the array entry is a VolatileNode wrapper (struct)
                    ClrValueType volatileNode = bucketsArray.GetStructValue(i);
                    node = volatileNode.ReadObjectField("_node");
                }
                else
                {
                    // For older runtimes, the array entry is the Node object
                    node = bucketsArray.GetObjectValue(i);
                }
                IClrValue keyField, valueField;
                if (!node.IsNull && node.IsValid)
                {
                    keyField = GetFieldFrom(node, keyFieldName);
                    valueField = GetFieldFrom(node, valueFieldName);

                    if (keyField == null || valueField == null)
                    {
                        throw new InvalidOperationException($"Concurrent dictionary doesn't contain {keyFieldName} and {valueFieldName} attributes. This command may not be compatible with the current runtime");
                    }
                }
                // Node is at the head object of a linked list
                while (!node.IsNull && node.IsValid)
                {
                    string key = DumpPropertyValue(node, keyFieldName);
                    string value = DumpPropertyValue(node, valueFieldName);

                    yield return new KeyValuePair<string, string>(key, value);

                    node = node.ReadObjectField(nextFieldName);
                }
            }
        }

        public static IClrValue GetFieldFrom(IClrValue entity, string fieldName)
        {
            IClrType entityType = entity?.Type ?? throw new ArgumentNullException(nameof(entity), "No associated type");
            IClrInstanceField field = entityType.GetFieldByName(fieldName) ?? throw new ArgumentException($"Type '{entityType}' does not contain a field named '{fieldName}'");
            return field.IsObjectReference ? entity.ReadObjectField(fieldName) : entity.ReadValueTypeField(fieldName);
        }

        public IEnumerable<ClrObject> EnumerateObjectsInGeneration(GCGeneration generation)
        {
            foreach (ClrSegment segment in _heap.Segments)
            {
                if (!TryGetSegmentMemoryRange(segment, generation, out ulong start, out ulong end))
                {
                    continue;
                }

                foreach (ClrObject obj in _heap.EnumerateObjects(new MemoryRange(start, end)))
                {
                    if (obj.IsValid)
                    {
                        yield return obj;
                    }
                }
            }
        }

        private static bool TryGetSegmentMemoryRange(ClrSegment segment, GCGeneration generation, out ulong start, out ulong end)
        {
            start = 0;
            end = 0;
            switch (generation)
            {
                case GCGeneration.Generation0:
                    if (segment.Kind == GCSegmentKind.Generation0 || segment.Kind == GCSegmentKind.Ephemeral)
                    {
                        start = segment.Generation0.Start;
                        end = segment.Generation0.End;
                    }
                    break;
                case GCGeneration.Generation1:
                    if (segment.Kind == GCSegmentKind.Generation1 || segment.Kind == GCSegmentKind.Ephemeral)
                    {
                        start = segment.Generation1.Start;
                        end = segment.Generation1.End;
                    }
                    break;
                case GCGeneration.Generation2:
                    if (segment.Kind == GCSegmentKind.Generation2 || segment.Kind == GCSegmentKind.Ephemeral)
                    {
                        start = segment.Generation2.Start;
                        end = segment.Generation2.End;
                    }
                    break;
                case GCGeneration.LargeObjectHeap:
                    if (segment.Kind == GCSegmentKind.Large)
                    {
                        start = segment.Start;
                        end = segment.End;
                    }
                    break;
                case GCGeneration.PinnedObjectHeap:
                    if (segment.Kind == GCSegmentKind.Pinned)
                    {
                        start = segment.Start;
                        end = segment.End;
                    }
                    break;
                case GCGeneration.FrozenObjectHeap:
                    if (segment.Kind == GCSegmentKind.Frozen)
                    {
                        start = segment.Start;
                        end = segment.End;
                    }
                    break;
                default:
                    return false;
            }
            return start != end;
        }

        public IEnumerable<string> EnumerateConcurrentQueue(ulong address)
        {
            return IsNetCore() ? EnumerateConcurrentQueueCore(address) : EnumerateConcurrentQueueFramework(address);
        }

        private IEnumerable<string> EnumerateConcurrentQueueCore(ulong address)
        {
            // A ConcurrentQueue<T> contains a linked list of Segment starting from _head
            // and each Segment contains an array of Slot<T> value type in _slots
            // Each Slot<T> is a value type that contains the real T in its Item field
            bool itemIsValueType = true;
            ClrType itemType = null;
            ClrObject cq = _heap.GetObject(address);
            ClrObject currentSegment = cq.ReadObjectField("_head");
            while (!currentSegment.IsNull && currentSegment.IsValid)
            {
                ClrArray slots = currentSegment.ReadObjectField("_slots").AsArray();
                if (itemType == null)
                {
                    itemType = _heap.GetObjectType(slots.Address).ComponentType;
                    if (itemType == null)
                    {
                        yield return $"dumparray {slots.Address:x16}";

                        currentSegment = currentSegment.ReadObjectField("_nextSegment");
                        continue;
                    }

                    // look for the first slot Item field that contains the T value
                    // could be a reference or a value type
                    ClrValueType slot = slots.GetStructValue(0);
                    if (!slot.IsValid)
                    {
                        yield return $"dumparray {slots.Address:x16}";

                        currentSegment = currentSegment.ReadObjectField("_nextSegment");
                        continue;
                    }

                    ClrInstanceField itemField = slot.Type.GetFieldByName("Item");
                    itemType = itemField.Type;

                    if (itemType == null)
                    {
                        yield return $"dumparray {slots.Address:x16}";

                        currentSegment = currentSegment.ReadObjectField("_nextSegment");
                        continue;
                    }

                    // canonical implementation is for reference types
                    itemIsValueType = (itemType.Name != "System.__Canon");

                    // in case of non simple value type, no way to get the values
                    // so just dump the array
                    if (itemIsValueType && !IsSimpleType(itemType.Name))
                    {
                        yield return $"dumparray {slots.Address:x16}";

                        currentSegment = currentSegment.ReadObjectField("_nextSegment");
                        continue;
                    }
                }

                int count = slots.Length;
                for (int current = 0; current < count; current++)
                {
                    ClrValueType slot = slots.GetStructValue(current);
                    if (itemIsValueType)
                    {
                        if (TryGetSimpleValue(slot, itemType, "Item", out string content))
                        {
                            yield return content;
                        }
                        else
                        {
                            // in case of non simple type, only the address of the array of slots is shown
                            yield return $"dumparray {slots.Address:x16}";

                            currentSegment = currentSegment.ReadObjectField("_nextSegment");
                            break;
                        }
                    }
                    else
                    {
                        // Item is holding the reference to the object in the heap
                        UIntPtr slotEntry = slot.ReadField<UIntPtr>("Item");

                        // skip empty null slots
                        if (slotEntry == UIntPtr.Zero)
                        {
                            continue;
                        }

                        ClrType slotType = _heap.GetObjectType(slotEntry.ToUInt64());
                        if (slotType.IsString)
                        {
                            yield return $"\"{_heap.GetObject(slotEntry.ToUInt64(), slotType).AsString()}\"";
                        }
                        else
                        {
                            yield return $"dumpobj 0x{slotEntry.ToUInt64():x16}";
                        }

                    }
                }

                currentSegment = currentSegment.ReadObjectField("_nextSegment");
            };
        }

        private IEnumerable<string> EnumerateConcurrentQueueFramework(ulong address)
        {
            // A ConcurrentQueue<T> contains a linked list of Segment starting from m_head
            // and each Segment contains an array of T in m_array
            bool itemIsValueType = true;
            ClrObject cq = _heap.GetObject(address);
            ClrType itemType = null;
            ClrObject currentSegment = cq.ReadObjectField("m_head");
            while (!currentSegment.IsNull && currentSegment.IsValid)
            {
                ClrArray items = currentSegment.ReadObjectField("m_array").AsArray();
                if (itemType == null)
                {
                    itemType = _heap.GetObjectType(items.Address)?.ComponentType;

                    // nothing more can be done than just dumping the array
                    if (itemType == null)
                    {
                        yield return $"dumparray 0x{items.Address:x16}";
                        currentSegment = currentSegment.ReadObjectField("m_next");
                        continue;
                    }

                    itemIsValueType = itemType.IsValueType;
                }

                int count = items.Length;
                for (int current = 0; current < count; current++)
                {
                    if (itemIsValueType)
                    {
                        ClrValueType item = items.GetStructValue(current);

                        // display value for simple types such as numbers and bool
                        if ((item.Type != null) && HasSimpleValue(items, current, item, out string content))
                        {
                            yield return content;
                        }
                        else
                        {
                            yield return $"dumpvc 0x{itemType.MethodTable:x16} 0x{item.Address:x16}";
                        }
                    }
                    else
                    {
                        ClrObject item = items.GetObjectValue(current);
                        if (item.IsNull || !item.IsValid)
                        {
                            // skip null reference special case
                            continue;
                        }
                        else
                        {
                            // reference type

                            // check automatically marshaled types
                            if (item.Type.IsString)
                            {
                                yield return $"\"{item.AsString()}\"";
                            }
                            else
                            {
                                yield return $"dumpobj 0x{item.Address:x16}";
                            }
                        }
                    }
                }

                currentSegment = currentSegment.ReadObjectField("m_next");
            }
        }

        private static bool IsSimpleType(string typeName)
        {
            switch (typeName)
            {
                case "System.Char":
                case "System.Boolean":
                case "System.SByte":
                case "System.Byte":
                case "System.Int16":
                case "System.UInt16":
                case "System.Int32":
                case "System.UInt32":
                case "System.Int64":
                case "System.UInt64":
                case "System.Single":
                case "System.Double":
                case "System.IntPtr":
                case "System.UIntPtr":
                    return true;

                default:
                    return false;
            }
        }

        private string DumpPropertyValue(ClrObject obj, string propertyName)
        {
            const string defaultContent = "?";

            IClrValue field = GetFieldFrom(obj, propertyName);
            if (field.Type is ClrType fieldType)
            {
                if (fieldType.IsString)
                {
                    return $"\"{_heap.GetObject(field.Address, fieldType).AsString()}\"";
                }
                else if (fieldType.IsArray)
                {
                    return $"dumparray {field.Address:x16}";
                }
                else if (fieldType.IsObjectReference)
                {
                    return $"dumpobj {field.Address:x16}";
                }
                else if (IsSimpleType(fieldType.Name) && TryGetSimpleValue(obj, fieldType, propertyName, out string simpleValuecontent))
                {
                    return simpleValuecontent;
                }
                else if (fieldType.IsValueType)
                {
                    return $"dumpvc {fieldType.MethodTable:x16} {field.Address:x16}";
                }
            }
            else
            {
                if (field is ClrObject objectField && objectField.IsNull)
                {
                    return "null";
                }

                return defaultContent;
            }
            return defaultContent;
        }

        private static bool HasSimpleValue(ClrArray items, int index, ClrValueType item, out string content)
        {
            content = null;
            string typeName = item.Type.Name;
            switch (typeName)
            {
                case "System.Char":
                    content = $"'{items.GetValue<char>(index)}'";
                    break;
                case "System.Boolean":
                    content = items.GetValue<bool>(index).ToString();
                    break;
                case "System.SByte":
                    content = items.GetValue<sbyte>(index).ToString();
                    break;
                case "System.Byte":
                    content = items.GetValue<byte>(index).ToString();
                    break;
                case "System.Int16":
                    content = items.GetValue<short>(index).ToString();
                    break;
                case "System.UInt16":
                    content = items.GetValue<ushort>(index).ToString();
                    break;
                case "System.Int32":
                    content = items.GetValue<int>(index).ToString();
                    break;
                case "System.UInt32":
                    content = items.GetValue<uint>(index).ToString();
                    break;
                case "System.Int64":
                    content = items.GetValue<long>(index).ToString();
                    break;
                case "System.UInt64":
                    content = items.GetValue<ulong>(index).ToString();
                    break;
                case "System.Single":
                    content = items.GetValue<float>(index).ToString();
                    break;
                case "System.Double":
                    content = items.GetValue<double>(index).ToString();
                    break;
                case "System.IntPtr":
                    {
                        IntPtr val = items.GetValue<IntPtr>(index);
                        content = (val == IntPtr.Zero) ? "null" : $"0x{val.ToInt64():x16}";
                    }
                    break;
                case "System.UIntPtr":
                    {
                        UIntPtr val = items.GetValue<UIntPtr>(index);
                        content = (val == UIntPtr.Zero) ? "null" : $"0x{val.ToUInt64():x16}";
                    }
                    break;

                default:
                    return false;
            }

            return true;
        }

        private static bool TryGetSimpleValue(IClrValue item, ClrType type, string fieldName, out string content)
        {
            content = null;
            string typeName = type.Name;
            switch (typeName)
            {
                case "System.Char":
                    content = $"'{item.ReadField<char>(fieldName)}'";
                    break;
                case "System.Boolean":
                    content = item.ReadField<bool>(fieldName).ToString();
                    break;
                case "System.SByte":
                    content = item.ReadField<sbyte>(fieldName).ToString();
                    break;
                case "System.Byte":
                    content = item.ReadField<byte>(fieldName).ToString();
                    break;
                case "System.Int16":
                    content = item.ReadField<short>(fieldName).ToString();
                    break;
                case "System.UInt16":
                    content = item.ReadField<ushort>(fieldName).ToString();
                    break;
                case "System.Int32":
                    content = item.ReadField<int>(fieldName).ToString();
                    break;
                case "System.UInt32":
                    content = item.ReadField<uint>(fieldName).ToString();
                    break;
                case "System.Int64":
                    content = item.ReadField<long>(fieldName).ToString();
                    break;
                case "System.UInt64":
                    content = item.ReadField<ulong>(fieldName).ToString();
                    break;
                case "System.Single":
                    content = item.ReadField<float>(fieldName).ToString();
                    break;
                case "System.Double":
                    content = item.ReadField<double>(fieldName).ToString();
                    break;
                case "System.IntPtr":
                    {
                        IntPtr val = item.ReadField<IntPtr>(fieldName);
                        content = (val == IntPtr.Zero) ? "null" : $"0x{val.ToInt64():x}";
                    }
                    break;
                case "System.UIntPtr":
                    {
                        UIntPtr val = item.ReadField<UIntPtr>(fieldName);
                        content = (val == UIntPtr.Zero) ? "null" : $"0x{val.ToUInt64():x}";
                    }
                    break;

                default:
                    return false;
            }

            return true;
        }

        public ClrModule GetMscorlib()
        {
            ClrModule bclModule = _clr.BaseClassLibrary;
            return bclModule;
        }

        public bool IsNetCore()
        {
            ClrModule coreLib = GetMscorlib();
            if (coreLib == null)
            {
                throw new InvalidOperationException("Impossible to find core library");
            }

            return (coreLib.Name.ToLowerInvariant().Contains("corelib"));
        }

        public bool Is64Bits()
        {
            return _clr.DataTarget.DataReader.PointerSize == 8;
        }
    }
}
