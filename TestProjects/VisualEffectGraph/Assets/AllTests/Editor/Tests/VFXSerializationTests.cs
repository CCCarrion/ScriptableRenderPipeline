using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Graphing;
using UnityEditor.VFX;

using Object = UnityEngine.Object;

namespace UnityEditor.VFX.Test
{
    [TestFixture]
    public class VFXSerializationTests
    {
        private readonly static string kTestAssetDir = "Assets/VFXEditor/Editor/Tests";
        private readonly static string kTestAssetName = "TestAsset";
        private readonly static string kTestAssetPath = kTestAssetDir + "/" + kTestAssetName + ".asset";

        private VFXAsset CreateAssetAtPath(string path)
        {
            VFXAsset asset = new VFXAsset();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        [OneTimeSetUpAttribute]
        public void OneTimeSetUpAttribute()
        {
            string[] guids = AssetDatabase.FindAssets(kTestAssetName, new string[] { kTestAssetDir });

            // If the asset does not exist, create it
            if (guids.Length == 0)
            {
                VFXAsset asset = CreateAssetAtPath(kTestAssetPath);
                InitAsset(asset);
                asset.UpdateSubAssets();
            }
        }

        [Test]
        public void SerializeModel()
        {
            VFXAsset assetSrc = new VFXAsset();
            VFXAsset assetDst = new VFXAsset();

            InitAsset(assetSrc);
            EditorUtility.CopySerialized(assetSrc, assetDst);
            CheckAsset(assetDst);

            Object.DestroyImmediate(assetSrc);
            Object.DestroyImmediate(assetDst);
        }

        [Test]
        public void LoadAssetFromPath()
        {
            VFXAsset asset = AssetDatabase.LoadAssetAtPath<VFXAsset>(kTestAssetPath);
            CheckAsset(asset);
        }

        private void InitAsset(VFXAsset asset)
        {
            var graph = asset.GetOrCreateGraph();
            graph.RemoveAllChildren();

            var init0 = ScriptableObject.CreateInstance<VFXBasicInitialize>();
            var update0 = ScriptableObject.CreateInstance<VFXBasicUpdate>();
            var output0 = ScriptableObject.CreateInstance<VFXBasicOutput>();

            graph.AddChild(init0);
            graph.AddChild(update0);
            graph.AddChild(output0);

            init0.LinkTo(update0);
            update0.LinkTo(output0);

            var init1 = ScriptableObject.CreateInstance<VFXBasicInitialize>();
            var output1 = ScriptableObject.CreateInstance<VFXBasicOutput>();

            init1.LinkTo(output1);

            graph.AddChild(init1);
            graph.AddChild(output1);

            // Add some block
            var block0 = ScriptableObject.CreateInstance<VFXInitBlockTest>();
            var block1 = ScriptableObject.CreateInstance<VFXUpdateBlockTest>();
            var block2 = ScriptableObject.CreateInstance<VFXOutputBlockTest>();

            // Add some operator
            VFXOperator add = ScriptableObject.CreateInstance<VFXOperatorAdd>();

            init0.AddChild(block0);
            update0.AddChild(block1);
            output0.AddChild(block2);

            graph.AddChild(add);
        }

        private void CheckAsset(VFXAsset asset)
        {
            VFXGraph graph = asset.GetOrCreateGraph();

            Assert.AreEqual(6, graph.GetNbChildren());

            Assert.AreEqual(1, graph[0].GetNbChildren());
            Assert.AreEqual(1, graph[1].GetNbChildren());
            Assert.AreEqual(1, graph[2].GetNbChildren());
            Assert.AreEqual(0, graph[3].GetNbChildren());
            Assert.AreEqual(0, graph[4].GetNbChildren());
            Assert.AreEqual(0, graph[5].GetNbChildren());

            Assert.IsNotNull((graph[0]).GetChild(0));
            Assert.IsNotNull((graph[1]).GetChild(0));
            Assert.IsNotNull((graph[2]).GetChild(0));

            Assert.AreEqual(VFXContextType.kInit,   ((VFXContext)(graph[0])).contextType);
            Assert.AreEqual(VFXContextType.kUpdate, ((VFXContext)(graph[1])).contextType);
            Assert.AreEqual(VFXContextType.kOutput, ((VFXContext)(graph[2])).contextType);
            Assert.AreEqual(VFXContextType.kInit,   ((VFXContext)(graph[3])).contextType);
            Assert.AreEqual(VFXContextType.kOutput, ((VFXContext)(graph[4])).contextType);

            Assert.IsNotNull(graph[5] as VFXOperatorAdd);
        }

        private void CheckIsolatedOperatorAdd(VFXOperatorAdd add)
        {
            Assert.AreEqual(1, add.outputSlots.Count);
            Assert.AreEqual(2, add.inputSlots.Count);
            Assert.AreEqual(typeof(FloatN), add.inputSlots[0].property.type);
            Assert.AreEqual(typeof(FloatN), add.inputSlots[1].property.type);
            Assert.AreEqual(typeof(float), add.outputSlots[0].property.type);
            Assert.IsNotNull(add.outputSlots[0].GetExpression());
            Assert.IsNotNull(add.outputSlots[0].GetExpression() as VFXExpressionAdd);
        }

        private void CheckIsolatedOperatorAbs(VFXOperatorAbsolute add)
        {
            Assert.AreEqual(1, add.outputSlots.Count);
            Assert.AreEqual(1, add.inputSlots.Count);
            Assert.AreEqual(typeof(FloatN), add.inputSlots[0].property.type);
            Assert.AreEqual(typeof(float), add.outputSlots[0].property.type);
            Assert.IsNotNull(add.outputSlots[0].GetExpression());
            Assert.IsNotNull(add.outputSlots[0].GetExpression() as VFXExpressionAbs);
        }

        private void CheckConnectedAbs(VFXOperatorAbsolute abs)
        {
            Assert.IsTrue(abs.inputSlots[0].HasLink());
            Assert.AreEqual(1, abs.inputSlots[0].LinkedSlots.Count());
            Assert.IsTrue(abs.inputSlots[0].GetExpression() is VFXExpressionAdd);
        }

        private void InnerSaveAndReloadTest(string suffixname, Action<VFXAsset> write, Action<VFXAsset> read)
        {
            var kTempAssetPathA = string.Format("{0}/Temp_{1}_A.asset", kTestAssetDir, suffixname);
            var kTempAssetPathB = string.Format("{0}/Temp_{1}_B.asset", kTestAssetDir, suffixname);
            AssetDatabase.DeleteAsset(kTempAssetPathA);
            AssetDatabase.DeleteAsset(kTempAssetPathB);

            int hashCodeAsset = 0; //check reference are different between load & reload
            {
                var asset = CreateAssetAtPath(kTempAssetPathA);

                hashCodeAsset = asset.GetHashCode();

                write(asset);
                asset.UpdateSubAssets();

                AssetDatabase.SaveAssets();

                asset = null;
                EditorUtility.UnloadUnusedAssetsImmediate();
                AssetDatabase.CopyAsset(kTempAssetPathA, kTempAssetPathB);
                AssetDatabase.RemoveObject(asset);
            }
            AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
            EditorUtility.UnloadUnusedAssetsImmediate();
            {
                VFXAsset asset = AssetDatabase.LoadAssetAtPath<VFXAsset>(kTempAssetPathB);
                Assert.AreNotEqual(hashCodeAsset, asset.GetHashCode());

                read(asset);
            }
            AssetDatabase.DeleteAsset(kTempAssetPathA);
            AssetDatabase.DeleteAsset(kTempAssetPathB);
        }

        private void WriteBasicOperators(VFXAsset asset, bool spawnAbs, bool linkAbs)
        {
            var add = ScriptableObject.CreateInstance<VFXOperatorAdd>();
            var graph = asset.GetOrCreateGraph();
            graph.AddChild(add);

            CheckIsolatedOperatorAdd(add);

            if (spawnAbs)
            {
                var abs = ScriptableObject.CreateInstance<VFXOperatorAbsolute>();
                abs.position = new Vector2(64.0f, 64.0f);
                graph.AddChild(abs);
                CheckIsolatedOperatorAbs(abs);
                if (linkAbs)
                {
                    abs.inputSlots[0].Link(add.outputSlots[0]);
                    CheckConnectedAbs(abs);
                }
            }
        }

        private void ReadBasicOperators(VFXAsset asset, bool spawnAbs, bool linkAbs)
        {
            var graph = asset.GetOrCreateGraph();
            Assert.AreEqual(spawnAbs ? 2 : 1, graph.GetNbChildren());
            Assert.IsNotNull((VFXOperatorAdd)graph[0]);
            var add = (VFXOperatorAdd)graph[0];
            CheckIsolatedOperatorAdd(add);

            if (spawnAbs)
            {
                Assert.IsNotNull((VFXOperatorAbsolute)graph[1]);
                var abs = (VFXOperatorAbsolute)graph[1];
                CheckIsolatedOperatorAbs(abs);
                Assert.AreEqual(abs.position.x, 64.0f);
                Assert.AreEqual(abs.position.y, 64.0f);
                if (linkAbs)
                {
                    CheckConnectedAbs(abs);
                }
            }
        }

        private void BasicOperatorTest(string suffix, bool spawnAbs, bool linkAbs)
        {
            InnerSaveAndReloadTest(suffix,
                (a) => WriteBasicOperators(a, spawnAbs, linkAbs),
                (a) => ReadBasicOperators(a, spawnAbs, linkAbs));
        }

        [Test]
        public void SerializeOneOperator()
        {
            BasicOperatorTest("One", false, false);
        }

        [Test]
        public void SerializeTwoOperators()
        {
            BasicOperatorTest("Two", true, false);
        }

        [Test]
        public void SerializeTwoOperatorsLink()
        {
            BasicOperatorTest("TwoLinked", true, true);
        }

        [Test]
        public void SerializeOperatorMaskWithState()
        {
            var expectedValue = new[] { VFXOperatorComponentMask.Component.X, VFXOperatorComponentMask.Component.Y, VFXOperatorComponentMask.Component.X };
            Action<VFXAsset> write = delegate(VFXAsset asset)
                {
                    var mask = ScriptableObject.CreateInstance<VFXOperatorComponentMask>();
                    mask.SetSettingValue("x", expectedValue[0]);
                    mask.SetSettingValue("y", expectedValue[1]);
                    mask.SetSettingValue("z", expectedValue[2]);

                    asset.GetOrCreateGraph().AddChild(mask);
                    Assert.AreEqual(expectedValue[0], (mask.settings as VFXOperatorComponentMask.Settings).x);
                    Assert.AreEqual(expectedValue[1], (mask.settings as VFXOperatorComponentMask.Settings).y);
                    Assert.AreEqual(expectedValue[2], (mask.settings as VFXOperatorComponentMask.Settings).z);
                };

            Action<VFXAsset> read = delegate(VFXAsset asset)
                {
                    var graph = asset.GetOrCreateGraph();
                    Assert.AreEqual(1, graph.GetNbChildren());
                    Assert.IsInstanceOf(typeof(VFXOperatorComponentMask), graph[0]);
                    var mask = graph[0] as VFXOperatorComponentMask;
                    Assert.IsInstanceOf(typeof(VFXOperatorComponentMask.Settings), mask.settings);

                    Assert.AreEqual(expectedValue[0], (mask.settings as VFXOperatorComponentMask.Settings).x);
                    Assert.AreEqual(expectedValue[1], (mask.settings as VFXOperatorComponentMask.Settings).y);
                    Assert.AreEqual(expectedValue[2], (mask.settings as VFXOperatorComponentMask.Settings).z);
                };

            InnerSaveAndReloadTest("Mask", write, read);
        }

        [Test]
        public void SerializeParameter()
        {
            var name = "unity";
            Action<VFXAsset> write = delegate(VFXAsset asset)
                {
                    var parameter = VFXLibrary.GetParameters().First(o => o.name == "Vector2").CreateInstance();
                    parameter.exposed = true;
                    parameter.exposedName = name;
                    asset.GetOrCreateGraph().AddChild(parameter);
                    Assert.AreEqual(VFXValueType.kFloat2, parameter.outputSlots[0].GetExpression().ValueType);
                };

            Action<VFXAsset> read = delegate(VFXAsset asset)
                {
                    var parameter = asset.GetOrCreateGraph()[0] as VFXParameter;
                    Assert.AreNotEqual(null, parameter);
                    Assert.AreEqual(true, parameter.exposed);
                    Assert.AreEqual(parameter.exposedName, name);
                    Assert.AreEqual(VFXValueType.kFloat2, parameter.outputSlots[0].GetExpression().ValueType);
                };

            InnerSaveAndReloadTest("Parameter", write, read);
        }

        [Test]
        public void SerializeOperatorAndParameter()
        {
            Action<VFXAsset> write = delegate(VFXAsset asset)
                {
                    var graph = asset.GetOrCreateGraph();
                    var add = ScriptableObject.CreateInstance<VFXOperatorAdd>();
                    var parameter = VFXLibrary.GetParameters().First(o => o.name == "Vector2").CreateInstance();
                    graph.AddChild(add);
                    graph.AddChild(parameter);
                    add.inputSlots[0].Link(parameter.outputSlots[0]);

                    Assert.AreEqual(VFXValueType.kFloat2, add.outputSlots[0].GetExpression().ValueType);
                };

            Action<VFXAsset> read = delegate(VFXAsset asset)
                {
                    var graph = asset.GetOrCreateGraph();
                    var add = graph[0] as VFXOperatorAdd;
                    var parameter = graph[1] as VFXParameter;
                    Assert.AreNotEqual(null, parameter);
                    Assert.AreEqual(VFXValueType.kFloat2, add.outputSlots[0].GetExpression().ValueType);
                };

            InnerSaveAndReloadTest("ParameterAndOperator", write, read);
        }

        [Test]
        public void SerializeBuiltInParameter()
        {
            Action<VFXAsset> write = delegate(VFXAsset asset)
                {
                    var builtIn = VFXLibrary.GetBuiltInParameters().First(o => o.name == VFXExpressionOp.kVFXTotalTimeOp.ToString()).CreateInstance();
                    asset.GetOrCreateGraph().AddChild(builtIn);
                    Assert.AreEqual(VFXExpressionOp.kVFXTotalTimeOp, builtIn.outputSlots[0].GetExpression().Operation);
                };

            Action<VFXAsset> read = delegate(VFXAsset asset)
                {
                    var builtIn = asset.GetOrCreateGraph()[0] as VFXBuiltInParameter;
                    Assert.AreNotEqual(null, builtIn);
                    Assert.AreEqual(VFXExpressionOp.kVFXTotalTimeOp, builtIn.outputSlots[0].GetExpression().Operation);
                };
            InnerSaveAndReloadTest("BuiltInParameter", write, read);
        }

        [Test]
        public void SerializeOperatorAndBuiltInParameter()
        {
            Action<VFXAsset> write = delegate(VFXAsset asset)
                {
                    var graph = asset.GetOrCreateGraph();
                    var add = ScriptableObject.CreateInstance<VFXOperatorAdd>();
                    var builtIn = VFXLibrary.GetBuiltInParameters().First(o => o.name == VFXExpressionOp.kVFXTotalTimeOp.ToString()).CreateInstance();
                    graph.AddChild(builtIn);
                    graph.AddChild(add);
                    add.inputSlots[0].Link(builtIn.outputSlots[0]);

                    Assert.AreEqual(VFXExpressionOp.kVFXTotalTimeOp, builtIn.outputSlots[0].GetExpression().Operation);
                    Assert.IsTrue(add.inputSlots[0].HasLink());
                };

            Action<VFXAsset> read = delegate(VFXAsset asset)
                {
                    var graph = asset.GetOrCreateGraph();
                    var builtIn = graph[0] as VFXBuiltInParameter;
                    var add = graph[1] as VFXOperatorAdd;

                    Assert.AreNotEqual(null, builtIn);
                    Assert.AreNotEqual(null, add);
                    Assert.AreEqual(VFXExpressionOp.kVFXTotalTimeOp, builtIn.outputSlots[0].GetExpression().Operation);
                    Assert.IsTrue(add.inputSlots[0].HasLink());
                };
            InnerSaveAndReloadTest("BuiltInParameter", write, read);
        }

        [Test]
        public void SerializeAttributeParameter()
        {
            var testAttribute = "size";
            Action<VFXAttributeParameter> test = delegate(VFXAttributeParameter parameter)
                {
                    Assert.AreEqual(VFXExpressionOp.kVFXNoneOp, parameter.outputSlots[0].GetExpression().Operation);
                    Assert.AreEqual(VFXValueType.kFloat2, parameter.outputSlots[0].GetExpression().ValueType);
                    Assert.IsInstanceOf(typeof(VFXAttributeExpression), parameter.outputSlots[0].GetExpression());
                    Assert.AreEqual(testAttribute, (parameter.outputSlots[0].GetExpression() as VFXAttributeExpression).attributeName);
                };

            Action<VFXAsset> write = delegate(VFXAsset asset)
                {
                    var size = VFXLibrary.GetAttributeParameters().First(o => o.name == testAttribute).CreateInstance();
                    asset.GetOrCreateGraph().AddChild(size);
                    test(size);
                };

            Action<VFXAsset> read = delegate(VFXAsset asset)
                {
                    var size = asset.GetOrCreateGraph()[0] as VFXAttributeParameter;
                    Assert.AreNotEqual(null, size);
                    test(size);
                };
            InnerSaveAndReloadTest("AttributeParameter", write, read);
        }
    }
}
