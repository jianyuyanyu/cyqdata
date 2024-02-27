using System;
using System.Collections.Generic;
using System.Text;
using System.Reflection.Emit;
using System.Reflection;
using CYQ.Data.Table;
using CYQ.Data.Tool;
using CYQ.Data.SQL;

namespace CYQ.Data.Emit
{
    /// <summary>
    /// 数据行转实体列表
    /// </summary>
    internal class ListStringToList
    {
        //public static object Test(List<string> dt, Type arrType, Type itemType)
        //{
        //    DynamicMethod method = CreateDynamicMethod(arrType, itemType);
        //    var func = method.CreateDelegate(typeof(Func<List<string>, object>)) as Func<List<string>, object>;
        //    var obj = func(dt);
        //    return obj;
        //}

        static Dictionary<Type, Func<List<string>, object>> typeFuncs = new Dictionary<Type, Func<List<string>, object>>();

        private static readonly object lockObj = new object();

        internal static Func<List<string>, object> Delegate(Type arrType, Type itemType)
        {
            if (typeFuncs.ContainsKey(arrType))
            {
                return typeFuncs[arrType];
            }
            lock (lockObj)
            {
                if (typeFuncs.ContainsKey(arrType))
                {
                    return typeFuncs[arrType];
                }
                DynamicMethod method = CreateDynamicMethod(arrType, itemType);
                var func = method.CreateDelegate(typeof(Func<List<string>, object>)) as Func<List<string>, object>;
                typeFuncs.Add(arrType, func);
                return func;
            }
        }

        private static DynamicMethod CreateDynamicMethod(Type arrType, Type itemType)
        {
            Type listStringType = typeof(List<string>);
            Type stringType = typeof(string);
            Type toolType = typeof(ConvertTool);
            var getCountMethod = listStringType.GetProperty("Count").GetGetMethod();
            var getStringMethod = listStringType.GetMethod("get_Item");
            var getChangeTypeMethod = toolType.GetMethod("ChangeType", BindingFlags.Static | BindingFlags.Public, null, new Type[] { typeof(object), typeof(Type) }, null);
            var getTypeFromHandleMethod = typeof(Type).GetMethod("GetTypeFromHandle");

            var method = new DynamicMethod("ListStringToList", typeof(object), new[] { listStringType }, itemType, true);

            var ilGen = method.GetILGenerator();

            // Define variables
            var countLocal = ilGen.DeclareLocal(typeof(int));
            var arrLocal = ilGen.DeclareLocal(arrType);
            var itemLocal = ilGen.DeclareLocal(itemType);
            var iLocal = ilGen.DeclareLocal(typeof(int));
            var stringLocal = ilGen.DeclareLocal(typeof(string));
            var itemTypeLocal = ilGen.DeclareLocal(typeof(Type));


            // Load the count of rows into the countLocal variable
            ilGen.Emit(OpCodes.Ldarg_0);
            ilGen.Emit(OpCodes.Callvirt, getCountMethod);
            ilGen.Emit(OpCodes.Stloc, countLocal);

            // Create a new instance of the target list type and store it in the itemsLocal variable
            if (arrType.Name.EndsWith("[]"))
            {
                ilGen.Emit(OpCodes.Ldloc, countLocal);
                ilGen.Emit(OpCodes.Newarr, itemType);
            }
            else
            {
                //外部MDataTable ToList GetArgumentLength 方法中已有处理，不过还是留着。
                if (arrType.Name == "IList`1")
                {
                    arrType = typeof(List<>).MakeGenericType(itemType);
                }
                ilGen.Emit(OpCodes.Newobj, arrType.GetConstructor(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, new Type[] { }, null));
            }
            ilGen.Emit(OpCodes.Stloc, arrLocal);

            // Initialize the iLocal variable to 0
            ilGen.Emit(OpCodes.Ldc_I4_0);
            ilGen.Emit(OpCodes.Stloc, iLocal);

            // Start loop
            var loopStart = ilGen.DefineLabel();
            ilGen.MarkLabel(loopStart);

            // Load the current row into the rowLocal variable
            ilGen.Emit(OpCodes.Ldarg_0);
            ilGen.Emit(OpCodes.Ldloc, iLocal);
            ilGen.Emit(OpCodes.Callvirt, getStringMethod);
            ilGen.Emit(OpCodes.Stloc, stringLocal);



            ilGen.Emit(OpCodes.Ldtoken, itemType);
            ilGen.Emit(OpCodes.Call, getTypeFromHandleMethod);
            ilGen.Emit(OpCodes.Stloc, itemTypeLocal);


            //Load the current row and the target type onto the stack and call the ToEntity method
            ilGen.Emit(OpCodes.Ldloc, stringLocal);
            ilGen.Emit(OpCodes.Ldloc, itemTypeLocal);
            ilGen.Emit(OpCodes.Call, getChangeTypeMethod);
            if(itemType.IsValueType)
            {
                ilGen.Emit(OpCodes.Unbox_Any, itemType);
            }

            ilGen.Emit(OpCodes.Stloc, itemLocal);

           
            // Assign the result to the itemsLocal array at the current index

            ilGen.Emit(OpCodes.Ldloc, arrLocal);
            if (arrType.Name.EndsWith("[]"))
            {  

                ilGen.Emit(OpCodes.Ldloc, iLocal);
                ilGen.Emit(OpCodes.Ldloc, itemLocal);
                if (itemType.IsValueType)
                {
                    ilGen.Emit(OpCodes.Stelem, itemType); // Box the value type
                }
                else
                {
                    ilGen.Emit(OpCodes.Stelem_Ref); // 存储引用类型
                }
            }
            else
            {
                ilGen.Emit(OpCodes.Ldloc, itemLocal);
                if (arrType.GetMethod("Add") != null)//List<T>
                {
                    ilGen.Emit(OpCodes.Callvirt, arrType.GetMethod("Add"));
                }
                else if (arrType.GetMethod("Push") != null)//State<T>
                {
                    ilGen.Emit(OpCodes.Callvirt, arrType.GetMethod("Push"));
                }
                else if (arrType.GetMethod("Enqueue") != null)//Queue<T>
                {
                    ilGen.Emit(OpCodes.Callvirt, arrType.GetMethod("Enqueue"));
                }
            }

            // Increment the index variable
            ilGen.Emit(OpCodes.Ldloc, iLocal);
            ilGen.Emit(OpCodes.Ldc_I4_1);
            ilGen.Emit(OpCodes.Add);
            ilGen.Emit(OpCodes.Stloc, iLocal);

            // Check if the loop should continue
            ilGen.Emit(OpCodes.Ldloc, iLocal);
            ilGen.Emit(OpCodes.Ldloc, countLocal);
            ilGen.Emit(OpCodes.Blt, loopStart);

            // Load the result array onto the stack and return it
            ilGen.Emit(OpCodes.Ldloc, arrLocal);
            ilGen.Emit(OpCodes.Ret);



            return method;
        }

    }
}
