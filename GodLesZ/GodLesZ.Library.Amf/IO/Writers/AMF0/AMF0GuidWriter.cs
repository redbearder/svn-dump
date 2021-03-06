using System;

namespace GodLesZ.Library.Amf.IO.Writers {
	/// <summary>
	/// This type supports the infrastructure and is not intended to be used directly from your code.
	/// </summary>
	class AMF0GuidWriter : IAMFWriter {
		public AMF0GuidWriter() {
		}

		#region IAMFWriter Members

		public bool IsPrimitive { get { return true; } }

		public void WriteData(AMFWriter writer, object data) {
			writer.WriteString(((Guid)data).ToString());
		}

		#endregion
	}
}
