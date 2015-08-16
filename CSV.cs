// CSV.cs
//
// A library to handle CSV format.
//
// Created by Austin Cathey
//
// Goal: To a create a CSV library which is RFC 4180 compliant.
//
// Required references:
//   System
//   WindowsBase
//

// Declare the namespace
namespace CSV
{
    // Namespaces required
    using System;
    using System.IO;
    using System.Text;

    // The base class for the reader and writer
    public abstract class CSVBase
    {
        private Stream m_stream = null;
        private int m_lineNo = 1;
        private int m_fieldNo = 1;

        // This class must be instantiated with a stream.
        protected CSVBase(Stream stream)
        {
            if ((object)stream == null || stream == Stream.Null)
            {
                throw new ArgumentNullException("A valid stream must be passed.");
            }
            m_stream = stream;
        }

        // Builds an exception string.
        protected string _exStr(string s)
        { /* Yeah, I could do this with a StringBuilder, but do
           * I really need performance to throw an exception? */
            return
                "\n*****\n" +
                s + "\n\n" +
                CurrentStatusString + "\n" +
                "*****";
        }
        protected string _exStr(string s, params object[] parms)
        { // To make my life easier.
            return _exStr(string.Format(s, parms));
        }

        // The close method
        public abstract void Close();

        // CanRead and CanWrite properties.
        // Since CSVBase is not associated with a reader
        // or writer, these are both false.
        public virtual bool CanRead
        {
            get { return false; }
        }
        public virtual bool CanWrite
        {
            get { return false; }
        }

        // The current line in the file.
        public int LineNumber
        {
            get { return m_lineNo; }
            protected set { m_lineNo = value; }
        }

        // The current field number, relative to the very beginning of the file.
        public int FieldNumber
        {
            get { return m_fieldNo; }
            protected set { m_fieldNo = value; }
        }

        // The BaseStream property
        public Stream BaseStream
        {
            get { return m_stream; }        // Simply return the associated stream.
        }

        // The Name property gets the associated filename.
        public string Name
        {
            get
            {
                FileStream fs = m_stream as FileStream;

                // Return the name of the file if the stream is associated with a file.
                if (fs != null)
                {
                    return fs.Name;
                }

                // Otherwise, notify the user that it's just a regular stream.
                return "**USER_STREAM**";
            }
        }

        // Get the info with the associated stream.
        public string CurrentStatusString
        {
            get
            {
                return string.Format("File:  {0}\nLine:  {1}\nField: {2}", Name, LineNumber, FieldNumber);
            }
        }
    }

    // A class to assist in reading, and to encapsulate such methods.
    public class CSVReaderHelper : CSVBase
    {
        protected StreamReader m_reader = null;     // protected because I want CSVReader to close since it's more
                                                    // capable to do so, due to how I chose to close it.
        private string m_readBuffer = null;
        private int m_readIndex;

        //
        // Constructors
        //

        // This class must have an associated stream.
        protected CSVReaderHelper(Stream stream)
            : base(stream)
        {
            m_reader = new StreamReader(stream);
        }

        //
        // Methods
        //

        // Close //////////////////////////////////////////
        public override void Close()
        {
            throw new NotImplementedException();
        }


        // getBuffer //////////////////////////////////////
        private WeakReference m_getBuffer_weakBuffer = new WeakReference(null); // static is not allowed in methods, so I
        private const int m_getBuffer_BUFFER_SIZE = 4096;                       // adopted this naming convention on my own.
        protected StringBuilder getBuffer()
        {
            StringBuilder sb = m_getBuffer_weakBuffer.Target as StringBuilder;
            if (sb == null)
            {
                sb = new StringBuilder(m_getBuffer_BUFFER_SIZE);
                m_getBuffer_weakBuffer.Target = sb;
            }
            return sb;
        }

        // _peek //////////////////////////////////////////
        protected int _peek()
        {
            // Read a line from the file if we don't have anything.
            if ((object)m_readBuffer == null)
            {
                m_readIndex = 0;
                m_readBuffer = m_reader.ReadLine();
                if ((object)m_readBuffer == null)
                {
                    return -1;
                }
            }

            // If the index is past the buffer, then that is a newline.
            if (m_readIndex >= m_readBuffer.Length)
            {
                return (int)'\n';
            }

            // Otherwise, get the character.
            else
            {
                return (int)m_readBuffer[m_readIndex];
            }
        }

        // _getc //////////////////////////////////////////
        protected int _getc()
        {
            int c = _peek();            // Get a character.

            // Don't do anything on EOF.
            if (c >= 0)
            {
                // Trigger a new read if we have reached the end of the line.
                if ((char)c == '\n')
                {
                    m_readBuffer = null;
                }

                // Update the position in the buffer.
                else
                {
                    m_readIndex++;
                }
            }

            // Return the character.
            return c;
        }

        // _move //////////////////////////////////////////
        protected int _move(int rel)
        { // Changes the relative position.  Returns the number of spaces actually moved.
            int sign = Math.Sign(rel);
            int max = Math.Abs(rel);
            int count;

            // Change the position.  We are doing it this way because of commas.
            for (count = 0; count < max; count++)
            {
                // Move the index in the indicated direction.
                m_readIndex += sign;

                // Bounds checking.
                if (m_readIndex < 0)
                {
                    m_readIndex = 0;
                    break;
                }
                else if (m_readIndex > m_readBuffer.Length)
                {
                    break;
                }

                // Update field counter accordingly if we reach a comma.
                else if (m_readBuffer[m_readIndex] == ',')
                {
                    FieldNumber += sign;
                }
            }

            // Return the number of spaces moved.
            return count;
        }

        //
        // Properties
        //

        // _readBuffer ////////////////////////////////////
        protected string _readBuffer
        {
            get { return m_readBuffer; }
        }

        // _readIndex /////////////////////////////////////
        protected int _readIndex
        {
            get { return m_readIndex; }
        }

        // CanRead ////////////////////////////////////////
        public override bool CanRead
        {
            get { return m_reader != null; }
        }
    }

    // The class for reading files.
    public class CSVReader : CSVReaderHelper, IDisposable
    {
        //
        // Constructors
        //

        // Attach to a stream
        public CSVReader(Stream stream) : base(stream) {}

        // Attach to a file
        public CSVReader(string filename) : this(new FileStream(filename, FileMode.Open)) {}

        //
        // Methods
        //

        // isFieldUpdater /////////////////////////////////
        private bool isFieldUpdater(char ch)
        {
            return ch == '\n' || ch == ',';
        }

        // getc ///////////////////////////////////////////
        private int getc(bool inQuotes)
        { /* Reads a character from the stream, updating necessary properties.
           * Note that FieldNumber can only be updated when inQuotes is false.
           */
            int c = _getc();

            // Do processing on a valid character.
            if (c >= 0)
            {
                // Update FieldNumber property if possible.
                if (isFieldUpdater((char)c) && !inQuotes)
                {
                    FieldNumber++;
                }

                // Update LineNumber property if we reach a newline.
                if ((char)c == '\n')
                {
                    LineNumber++;
                }
            }

            // Return the character.
            return c;
        }

        // getNextRawItem /////////////////////////////////
        private StringBuilder getNextRawItem()
        { // Gets the next item in the stream, without doing any processing of quote characters.
            bool inQuotes;
            int c, startField;
            StringBuilder buffer = getBuffer();

            // Get the current position in the file.
            startField = FieldNumber;

            //
            // Get the field.
            //

            // Initialize variables.
            buffer.Length = 0;          // This clears the buffer.  You don't want to directly assign a string to a StringBuilder.
            inQuotes = false;           // We can't be in quotes yet.

            // Get the data.
            //
            // Please note that getc() update LineNumber and FieldNumber.
            //
            c = getc(inQuotes);
            while (c >= 0 && (startField == FieldNumber))
            {
                // Toggle quote state if we are allowed to do so.
                if ((char)c == '\"')
                {
                    // Are we allowed to leave the quotes?
                    if (inQuotes)
                    {
                        // Append this quote now, since our next move depends on the next character.
                        buffer.Append((char)c);
                        c = _peek();

                        // If the next character is not a quote, we are at the
                        // end of our quoted string, and at the end of our field.
                        if ((char)c != '\"')
                        {
                            // We are no longer in quotes.  Notify our method
                            // so we don't throw an exception.
                            inQuotes = false;
                            c = getc(inQuotes);     // Discard character.

                            // However, this next character had better changed the field number, or else.
                            // Since this is the same test to see if we update the buffer,
                            // if this exception is not thrown, we also do not update the buffer.
                            //
                            if (startField == FieldNumber)
                            {
                                throw new FileFormatException(_exStr("Comma or newline expected"));
                            }
                        }

                        // If we make it here, the character is obviously a
                        // double quote.  The next executable statement will
                        // append it to the buffer.
                        else
                        {
                            getc(inQuotes);     // Discard quote.
                        }
                    }

                    // Put ourselves in quotes.
                    else
                    {
                        inQuotes = true;
                    }
                }

                // Only update the buffer and grab the next character
                // if we did not change fields.
                if (startField == FieldNumber)
                {
                    buffer.Append((char)c);
                    c = getc(inQuotes);
                }
            }

            // Throw an exception if we are still in quotes.
            if (inQuotes)
            {
                throw new FileFormatException(_exStr("Unexpected EOF in a quoted string"));
            }

            // If we hit an empty field, ensure it's not due to excessive line breaks.
            else if (buffer.Length == 0)
            {
                // Force an EOF, throwing an exception if we get anything other than a newline.
                while (c >= 0)
                {
                    if ((char)c != '\n')
                    {
                        throw new FileFormatException(_exStr("Empty field encountered."));
                    }
                    c = getc(false);
                }
            }

            // If we read something, then return it.
            if (buffer.Length > 0)
            {
                return buffer;
            }

            // We got here if we reached EOF or the last field.
            return null;
        }

        // convertRawItemToString /////////////////////////
        private string convertRawItemToString(StringBuilder sb)
        {
            // Don't bother messing with anything if we received nothing.
            if (sb == null)
            {
                return null;
            }

            // With the way getNextRawItem() works, if the string
            // begins with a quote, it must also end with a quote, so
            // checking the last character is not necessary.
            if (sb[0] == '\"')
            {
                sb.Remove(0, 1);
                sb.Remove(sb.Length - 1, 1);
            }

            // Replace all pairs of double quotes with a single double quote.
            sb.Replace("\"\"", "\"");

            // Return string.
            return sb.ToString();
        }

        // ReadString /////////////////////////////////////
        public string ReadString()
        { // Gets the next string.
            return convertRawItemToString(getNextRawItem());
        }

        // ReadInt16 //////////////////////////////////////
        public Int16 ReadInt16()
        { // Reads a 16-bit integer from the stream.
            return Int16.Parse(getNextRawItem().ToString());
        }

        // ReadInt32 //////////////////////////////////////
        public Int32 ReadInt32()
        { // Reads a 32-bit integer from the stream.
            return Int32.Parse(getNextRawItem().ToString());
        }

        // ReadInt64 //////////////////////////////////////
        public Int64 ReadInt64()
        { // Reads a 64-bit integer from the stream.
            return Int64.Parse(getNextRawItem().ToString());
        }

        // ReadUInt16 /////////////////////////////////////
        public UInt16 ReadUInt16()
        { // Reads an unsigned 16-bit integer from the stream.
            return UInt16.Parse(getNextRawItem().ToString());
        }

        // ReadUInt32 /////////////////////////////////////
        public UInt32 ReadUInt32()
        { // Reads an unsigned 32-bit integer from the stream.
            return UInt32.Parse(getNextRawItem().ToString());
        }

        // ReadUInt64 /////////////////////////////////////
        public UInt64 ReadUInt64()
        { // Reads a 64-bit integer from the stream.
            return UInt64.Parse(getNextRawItem().ToString());
        }

        // ReadByte ///////////////////////////////////////
        public Byte ReadByte()
        { // Reads a byte from the stream.
            return Byte.Parse(getNextRawItem().ToString());
        }

        // ReadSByte //////////////////////////////////////
        public SByte ReadSByte()
        { // Reads a signed byte from the stream.
            return SByte.Parse(getNextRawItem().ToString());
        }

        // ReadSingle /////////////////////////////////////
        public Single ReadSingle()
        { // Reads a 4-byte floating point number from the stream.
            return Single.Parse(getNextRawItem().ToString());
        }

        // ReadDouble /////////////////////////////////////
        public Double ReadDouble()
        { // Reads an 8-byte floating point number from the stream.
            return Double.Parse(getNextRawItem().ToString());
        }

        // ReadDecimal ////////////////////////////////////
        public Decimal ReadDecimal()
        { // Reads a 16-byte floating point number from the stream.
            return Decimal.Parse(getNextRawItem().ToString());
        }

        // ReadNext ///////////////////////////////////////
        public object ReadNext()
        { /* Gets the next item, regardless of type.
           * I am aware that this is not as efficient because of boxing.
           * 
           * If it is a number, a number is returned.
           * 
           * The idea behind this function is that I want "1234" to be evaluated as a string, and 1234 to be
           * evaluated as a number.
           */
            StringBuilder sb = getNextRawItem();
            string s;
            double dbl;
            long l;
            ulong ul;

            // If there is no item to work with, then get out to avoid an exception.
            if (sb == null)
            {
                return null;
            }

            // We don't need all them freaking function calls! =P
            s = sb.ToString();

            // Integral data
            if (long.TryParse(s, out l))
            {
                return (object)l;
            }
            else if (ulong.TryParse(s, out ul))
            {
                return (object)ul;
            }

            // Real data
            else if (double.TryParse(s, out dbl))
            {
                return (object)dbl;
            }

            // Ohterwise, string data.
            return convertRawItemToString(sb);
        }

        // ReadNext ///////////////////////////////////////
        public object ReadNext(Type type)
        {
            return Convert.ChangeType(ReadNext(), type);
        }

        // ReadNext ///////////////////////////////////////
        public object ReadNext(TypeCode typecode)
        {
            return Convert.ChangeType(ReadNext(), typecode);
        }

        //
        // Disposal routines
        //

        // Close //////////////////////////////////////////
        public override void Close()
        {
            Dispose();
        }

        // Dispose ////////////////////////////////////////
        private bool m_isDisposed = false;
        public void Dispose()
        {
            Dispose(true);
        }

        // Dispose ////////////////////////////////////////
        private void Dispose(bool disposing)
        { /* Right now there is no use for the disposing parameter,
           * but the convention is that when it is true, it means
           * that this method was invoked by directly calling
           * Dispose(), and if it's false, that means that this
           * method was invoked by the garbage collector, through
           * the destructor. */
            if (!m_isDisposed)
            {
                // Close the reader.
                if (m_reader != null)
                {
                    m_reader.Close();
                    m_reader = null;    // Detach the reference.
                }

                // Finalization not necessary if we are directly calling this.
                if (disposing)
                {
                    GC.SuppressFinalize(this);
                }

                // Okay, we are done.
                m_isDisposed = true;
            }
        }

        // ~CSVReader /////////////////////////////////////
        ~CSVReader()
        {
            Dispose(false);
        }
    }

    // The class for writing files.
    public class CSVWriter : CSVBase, IDisposable
    {
        private StreamWriter m_writer = null;           // Associated StreamWriter
        private bool m_startOfLine = true;              // Are we at the start of the line?  (i.e. before writing the first field)

        //
        // Constructors
        //

        // Attach to a stream
        public CSVWriter(Stream stream) : base(stream)
        {
            m_writer = new StreamWriter(stream);
        }

        // Attach to a file
        public CSVWriter(string filename, bool append) : this(new FileStream(filename, append ? FileMode.Append : FileMode.Create)) { }
        public CSVWriter(string filename) : this(filename, false) { }

        //
        // Methods
        //

        // EndLine ////////////////////////////////////////
        public void EndLine()
        { // Ends the current line and starts the next
            m_writer.WriteLine();
            m_startOfLine = true;
            LineNumber++;
        }

        // beginWrite /////////////////////////////////////
        private void beginWrite()
        { // Common code that all Write() methods should run.
            if (!m_startOfLine)
            {
                m_writer.Write(",");
            }
        }

        // endWrite ///////////////////////////////////////
        private void endWrite()
        { // Common code that all Write() methods should run.
            m_startOfLine = false;
            FieldNumber++;          // Next field.
        }

        // Write (string) /////////////////////////////////
        public void Write(string s)
        { // Writes a string to the file.  Newlines are not permitted, because end of line and end of field are the same thing.
            StringBuilder sb;

            // Initialize
            beginWrite();

            // A single comma should be replaced with a comma surrounded by quotes.
            if (s.Length == 1 && s[0] == ',')
            {
                s = "\",\"";
            }

            // An empty string should be replaced by a pair of double quotes.
            else if (s.Length == 0)
            {
                s = "\"\"";
            }

            // Otherwise, process any existing quotes.
            else
            {
                // Convert any quotes into pairs of double quotes.
                sb = new StringBuilder(s);
                sb.Replace("\"", "\"\"");

                // Surround the string with quotes if necessary.
                if (s.IndexOfAny(new char[] { '\n', '\"', ',' }) >= 0)
                {
                    sb.Insert(0, '\"');
                    sb.Append('\"');
                }

                // Get the reference of this new string.
                s = sb.ToString();
            }

            // Write the string.
            m_writer.Write(s);

            // Shutdown
            endWrite();
        }

        // Write (char[]) /////////////////////////////////
        public void Write(char[] c)
        {
            beginWrite();
            m_writer.Write(c);
            endWrite();
        }

        // Write (char) ///////////////////////////////////
        public void Write(char c)
        {
            beginWrite();
            m_writer.Write(c);
            endWrite();
        }

        // Write (int) ////////////////////////////////////
        public void Write(int n)
        {
            beginWrite();
            m_writer.Write(n);
            endWrite();
        }

        // Write (uint) ///////////////////////////////////
        public void Write(uint n)
        {
            beginWrite();
            m_writer.Write(n);
            endWrite();
        }

        // Write (long) ///////////////////////////////////
        public void Write(long n)
        {
            beginWrite();
            m_writer.Write(n);
            endWrite();
        }

        // Write (ulong) //////////////////////////////////
        public void Write(ulong n)
        {
            beginWrite();
            m_writer.Write(n);
            endWrite();
        }

        // Write (float) //////////////////////////////////
        public void Write(float n)
        {
            beginWrite();
            m_writer.Write(n);
            endWrite();
        }

        // Write (double) /////////////////////////////////
        public void Write(double n)
        {
            beginWrite();
            m_writer.Write(n);
            endWrite();
        }

        // Write (decimal) ////////////////////////////////
        public void Write(decimal n)
        {
            beginWrite();
            m_writer.Write(n);
            endWrite();
        }

        // Write (bool) ///////////////////////////////////
        public void Write(bool n)
        {
            beginWrite();
            m_writer.Write(n);
            endWrite();
        }

        // Close //////////////////////////////////////////
        public override void Close()
        { // Closes the file.
            Dispose();  // Release resources.
        }

        //
        // Properties
        //

        // CanWrite ///////////////////////////////////////
        public override bool CanWrite
        {
            get { return m_writer != null; }
        }

        //
        // Disposal routines
        //

        // Dispose ////////////////////////////////////////
        private bool m_isDisposed = false;
        public void Dispose()
        {
            Dispose(true);
        }

        // Dispose ////////////////////////////////////////
        private void Dispose(bool disposing)
        {
            if (!m_isDisposed)
            {
                // Close our StreamWriter.
                if (m_writer != null)
                {
                    // End the line if we haven't done so already.
                    if (!m_startOfLine)
                    {
                        EndLine();
                    }

                    // Close the file and detach the reference.
                    m_writer.Close();
                    m_writer = null;
                }

                // No need to go in the finalize queue.
                if (disposing)
                {
                    GC.SuppressFinalize(this);
                }

                // Finished disposing resources.
                m_isDisposed = true;
            }
        }

        // ~CSVWriter /////////////////////////////////////
        ~CSVWriter()
        {
            Dispose(false);
        }
    }
}