using System.Collections.Generic;

#pragma warning disable SA1649 // File name should match first type name
namespace HttpMultipartParser
{
	/// <summary>
	/// The FileStreamDelegate defining functions that can handle file stream data from this parser.
	///
	/// Delegates can assume that the data is sequential i.e. the data received by any delegates will be
	/// the data immediately following any previously received data.
	/// </summary>
	/// <param name="name">The name of the multipart data.</param>
	/// <param name="fileName">The name of the file.</param>
	/// <param name="contentType">The content type of the multipart data.</param>
	/// <param name="contentDisposition">The content disposition of the multipart data.</param>
	/// <param name="buffer">Some of the data from the file (not necessarily all of the data).</param>
	/// <param name="bytes">The length of data in buffer.</param>
	/// <param name="partNumber">Each chunk (or "part") in a given file is sequentially numbered, starting at zero.</param>
	/// <param name="additionalProperties">Properties other than the "well known" ones (such as name, filename, content-type, etc.) associated with a file stream.</param>
	public delegate void FileStreamDelegate(string name, string fileName, string contentType, string contentDisposition, byte[] buffer, int bytes, int partNumber, IDictionary<string, string> additionalProperties);

	/// <summary>
	/// The StreamClosedDelegate defining functions that can handle stream being closed.
	/// </summary>
	public delegate void StreamClosedDelegate();

	/// <summary>
	/// The ParameterDelegate defining functions that can handle multipart parameter data.
	/// </summary>
	/// <param name="part">The parsed parameter part.</param>
	public delegate void ParameterDelegate(ParameterPart part);

	/// <summary>
	/// The BinaryParameterDelegate defining functions that can handle multipart parameter data.
	/// </summary>
	/// <param name="binaryPart">The parsed parameter part.</param>
	public delegate void BinaryParameterDelegate(ParameterPartBinary binaryPart);
}
#pragma warning restore SA1649 // File name should match first type name
