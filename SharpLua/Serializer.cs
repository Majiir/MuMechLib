/*
 * Created by SharpDevelop.
 * User: elijah
 * Date: 12/26/2011
 * Time: 12:19 PM
 * 
 * To change this template use Tools | Options | Coding | Edit Standard Headers.
 */
using System;
using KSP.IO;
using SharpLua.AST;

namespace SharpLua
{
    /// <summary>
    /// Serializes an object. The object must have the Serializable() attribute.
    /// </summary>
    public class Serializer
    {
        public static void Serialize(object obj, string filename)
        {
            File.WriteAllBytes<Serializer>(IOUtils.SerializeToBinary(obj), filename);
        }
        
        public static object Deserialize(string filename)
        {
            return IOUtils.DeserializeFromBinary(File.ReadAllBytes<Serializer>(filename));
        }
    }
}
