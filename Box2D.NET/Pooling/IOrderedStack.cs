// ****************************************************************************
// Copyright (c) 2011, Daniel Murphy
// All rights reserved.
// 
// Redistribution and use in source and binary forms, with or without modification,
// are permitted provided that the following conditions are met:
// * Redistributions of source code must retain the above copyright notice,
// this list of conditions and the following disclaimer.
// * Redistributions in binary form must reproduce the above copyright notice,
// this list of conditions and the following disclaimer in the documentation
// and/or other materials provided with the distribution.
// 
// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND
// ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
// WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE DISCLAIMED.
// IN NO EVENT SHALL THE COPYRIGHT HOLDER OR CONTRIBUTORS BE LIABLE FOR ANY DIRECT,
// INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT
// NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR
// PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY,
// WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE)
// ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED OF THE
// POSSIBILITY OF SUCH DAMAGE.
// ****************************************************************************

namespace Box2D.Pooling
{

	/// <summary>
	/// This stack assumes that when you push 'n' items back,
	/// you're pushing back the last 'n' items popped.
	/// </summary>
	/// <author>Daniel</author>
	/// <typeparam name="T"></typeparam>
	public interface IOrderedStack<out T>
	{
		/// <summary>
		/// Returns the next object in the pool
		/// </summary>
		/// <returns></returns>
		T Pop();

		/// <summary>
		/// Returns the next 'argNum' objects in the pool in an array
		/// </summary>
		/// <param name="argNum"></param>
		/// <returns>an array containing the next pool objects in items 0-argNum. Array length and uniqueness not guaranteed.</returns>
		T[] Pop(int argNum);

		/// <summary>
		/// Tells the stack to take back the last 'argNum' items
		/// </summary>
		/// <param name="argNum"></param>
		void Push(int argNum);
	}
}