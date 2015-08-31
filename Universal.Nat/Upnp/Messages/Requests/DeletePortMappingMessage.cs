//
// Authors:
//   Alan McGovern alan.mcgovern@gmail.com
//
// Copyright (C) 2006 Alan McGovern
//
// Permission is hereby granted, free of charge, to any person obtaining
// a copy of this software and associated documentation files (the
// "Software"), to deal in the Software without restriction, including
// without limitation the rights to use, copy, modify, merge, publish,
// distribute, sublicense, and/or sell copies of the Software, and to
// permit persons to whom the Software is furnished to do so, subject to
// the following conditions:
// 
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
//

using System.Net;
using System.Text;
using Universal.Nat.Enums;

namespace Universal.Nat.Upnp.Messages.Requests
{
    internal class DeletePortMappingMessage : MessageBase
    {
        private readonly Mapping _mapping;

        public DeletePortMappingMessage(Mapping mapping, UpnpNatDevice device)
            : base(device)
        {
            _mapping = mapping;
        }

        public override WebRequest Encode(out byte[] body)
        {
            var builder = new StringBuilder(256);
            var writer = CreateWriter(builder);

            WriteFullElement(writer, "NewRemoteHost", string.Empty);
            WriteFullElement(writer, "NewExternalPort", _mapping.PublicPort.ToString(Culture));
            WriteFullElement(writer, "NewProtocol", _mapping.Protocol == Protocol.Tcp ? "TCP" : "UDP");

            writer.Flush();
            return CreateRequest("DeletePortMapping", builder.ToString(), out body);
        }
    }
}