/* Copyright (c) 2014 Oberon microsystems, Inc. (Switzerland)
 * 
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 * 
 * http://www.apache.org/licenses/LICENSE-2.0
 * 
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License. */

// Originally developed for the book
//   "Getting Started with the Internet of Things", by Cuno Pfister.
//   Copyright 2011 Cuno Pfister, Inc., 978-1-4493-9357-1.
//
// Version 4.3, for the .NET Micro Framework release 4.3.
//
// Microsoft has implemented a version of Design by Contract in .NET 4.5. This
// code file here adds a subset of Microsoft's API to the .NET Micro Framework.
// See also http://msdn.microsoft.com/en-us/library/dd264808.aspx

// Design by Contract is a way to precisely document the semantics of methods,
// and to make sure that the program behaves in the documented way at runtime.
//
// Design by contract uses the analogy of programminging interfaces with
// contracts, to specify the rights and obligations of someone who calls
// (or implements) a method:
//
// What do you have to guarantee before calling the method, what can you expect
// after the method has returned.
//
// Ideally, you never have to study a method's implementation, but can just rely
// on its contract. In our experience, it not only helps to improve code quality,
// but also makes programming more agile: it reduces the fear to make desirable
// changes to a program, because errors usually become evident very quickly and
// close to their root causes. In this way it is complementary to testing.
//
// For an introduction to Design by Contract by its inventor Bertrand Meyer, see
//   http://se.ethz.ch/~meyer/publications/computer/contract.pdf
//
// See Microsoft's API documentation for details:
// http://msdn.microsoft.com/en-us/library/system.diagnostics.contracts.contract(v=vs.110).aspx

using JetBrains.Annotations;

namespace System.Diagnostics.Contracts
{
    /// <summary>
    /// Contains static methods for representing program contracts such as preconditions,
    /// postconditions, and object invariants.
    /// </summary>
    public static class Contract
    {
        /// <summary>
        /// Specifies a precondition contract for the enclosing method or property.
        /// Throws an exception if a precondition is violated.
        /// </summary>
        /// <param name="condition">The conditional expression to test.</param>
        [ContractAnnotation("false => halt")]
        public static void Requires(bool condition)
        {
            if (!condition)
            {
                Trace.Fail("precondition failed");
                throw new Exception("precondition failed");
            }
        }

        /// <summary>
        /// Specifies a postcondition contract for the enclosing method or property.
        /// Throws an exception if a postcondition is violated.
        /// </summary>
        /// <param name="condition">The conditional expression to test.</param>
        public static void Ensures(bool condition)
        {
            if (!condition)
            {
                Trace.Fail("postcondition failed");
                throw new Exception("postcondition failed");
            }
        }

        /// <summary>
        /// Specifies an invariant contract for the enclosing method or property.
        /// Throws an exception if an invariant is violated,
        /// e.g., a loop invariant or object invariant.
        /// </summary>
        /// <param name="condition">The conditional expression to test.</param>
        public static void Invariant(bool condition)
        {
            if (!condition)
            {
                Trace.Fail("invariant failed");
                throw new Exception("invariant failed");
            }
        }

        /// <summary>
        /// Checks for a condition; if the condition is false, throws an exception.
        /// </summary>
        /// <param name="condition">The conditional expression to test.</param>
        public static void Assert(bool condition)
        {
            if (!condition)
            {
                Trace.Fail("assertion failed");
                throw new Exception("assertion failed");
            }
        }
    }
}
