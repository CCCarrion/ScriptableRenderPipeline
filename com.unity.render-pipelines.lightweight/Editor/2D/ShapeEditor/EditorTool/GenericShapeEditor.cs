using System;
using System.Collections.Generic;
using UnityEngine;

namespace UnityEditor.Experimental.Rendering.LWRP.Path2D
{
    internal class GenericShapeEditor<T> : ScriptableShapeEditor
    {
        [SerializeField]
        private List<T> m_Data = new List<T>();

        public T[] data
        {
            get { return m_Data.ToArray(); }
            set
            {
                if (value.Length != pointCount)
                    throw new Exception("Custom data count does not match control point count");
                
                m_Data.Clear();
                m_Data.AddRange(value);
            }
        }

        public override void Clear()
        {
            base.Clear();

            m_Data.Clear();
        }

        public override void AddPoint(ControlPoint controlPoint)
        {
            base.AddPoint(controlPoint);

            m_Data.Add(Create());
        }

        public override void InsertPoint(int index, ControlPoint controlPoint)
        {
            base.InsertPoint(index, controlPoint);

            m_Data.Insert(index, Create());
        }

        public override void RemovePoint(int index)
        {
            base.RemovePoint(index);

            Destroy(m_Data[index]);

            m_Data.RemoveAt(index);
        }

        protected virtual T Create()
        {
            return Activator.CreateInstance<T>();
        }

        protected virtual void Destroy(T data) { }
    }
}
