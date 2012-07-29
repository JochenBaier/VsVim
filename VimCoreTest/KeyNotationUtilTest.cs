﻿using System;
using System.Linq;
using Vim.Extensions;
using Xunit;

namespace Vim.UnitTest
{
    public abstract class KeyNotationUtilTest
    {
        protected static void AssertSingle(string input, VimKey? key = null)
        {
            AssertSingle(input, key.HasValue ? KeyInputUtil.VimKeyToKeyInput(key.Value) : null);
        }

        protected static void AssertSingle(string input, KeyInput expected = null)
        {
            var opt = KeyNotationUtil.TryStringToKeyInput(input);
            if (expected != null)
            {
                Assert.True(opt.IsSome());
                Assert.Equal(expected, opt.Value);
                Assert.Equal(expected, KeyNotationUtil.StringToKeyInput(input));
            }
            else
            {
                Assert.True(opt.IsNone());
            }
        }

        protected static void AssertMany(string input, string result)
        {
            AssertMany(input, KeyInputSetUtil.OfString(result));
        }

        protected static void AssertMany(string input, KeyInputSet keyInputSet)
        {
            var opt = KeyNotationUtil.TryStringToKeyInputSet(input);
            Assert.True(opt.IsSome());
            Assert.Equal(opt.Value, keyInputSet);
        }

        public sealed class Single : KeyNotationUtilTest
        {
            [Fact]
            public void LessThanChar()
            {
                AssertSingle("<", VimKey.LessThan);
            }

            [Fact]
            public void LeftKey()
            {
                AssertSingle("<Left>", VimKey.Left);
            }

            [Fact]
            public void RightKey()
            {
                AssertSingle("<Right>", VimKey.Right);
                AssertSingle("<rIGht>", VimKey.Right);
            }

            [Fact]
            public void ShiftAlphaShouldPromote()
            {
                AssertSingle("<S-A>", VimKey.UpperA);
                AssertSingle("<s-a>", VimKey.UpperA);
            }

            [Fact]
            public void AlphaAloneIsCaseSensitive()
            {
                AssertSingle("a", VimKey.LowerA);
                AssertSingle("A", VimKey.UpperA);
            }

            [Fact]
            public void ShiftNumberShouldNotPromote()
            {
                AssertSingle("<S-1>", KeyInputUtil.ApplyModifiersToVimKey(VimKey.Number1, KeyModifiers.Shift));
                AssertSingle("<s-1>", KeyInputUtil.ApplyModifiersToVimKey(VimKey.Number1, KeyModifiers.Shift));
            }

            [Fact]
            public void AlphaWithControl()
            {
                AssertSingle("<C-x>", KeyInputUtil.ApplyModifiersToVimKey(VimKey.LowerX, KeyModifiers.Control));
                AssertSingle("<c-X>", KeyInputUtil.ApplyModifiersToVimKey(VimKey.UpperX, KeyModifiers.Control));
            }

            [Fact]
            public void AlphaWithAltIsCaseSensitive()
            {
                AssertSingle("<A-b>", KeyInputUtil.ApplyModifiersToVimKey(VimKey.LowerB, KeyModifiers.Alt));
                AssertSingle("<A-B>", KeyInputUtil.ApplyModifiersToVimKey(VimKey.UpperB, KeyModifiers.Alt));
            }

            [Fact]
            public void DontMapControlPrefixAsSingleKey()
            {
                AssertSingle("CTRL-x", expected: null);
            }

            [Fact]
            public void NotationControlAndSymbol()
            {
                AssertSingle("<C-]>", KeyInputUtil.ApplyModifiersToVimKey(VimKey.CloseBracket, KeyModifiers.Control));
            }

            [Fact]
            public void NotationOfFunctionKey()
            {
                AssertSingle("<S-F11>", KeyInputUtil.ApplyModifiersToVimKey(VimKey.F11, KeyModifiers.Shift));
                AssertSingle("<c-F11>", KeyInputUtil.ApplyModifiersToVimKey(VimKey.F11, KeyModifiers.Control));
            }

            [Fact]
            public void ShiftAndControlModifier()
            {
                AssertSingle("<C-S-A>", KeyInputUtil.ApplyModifiersToVimKey(VimKey.UpperA, KeyModifiers.Control));
            }

            [Fact]
            public void BackslashLiteral()
            {
                AssertSingle(@"\", VimKey.Backslash);
            }

            /// <summary>
            /// Case shouldn't matter
            /// </summary>
            [Fact]
            public void CaseShouldntMatter()
            {
                var ki = KeyInputUtil.EscapeKey;
                var all = new string[] { "<ESC>", "<esc>", "<Esc>" };
                foreach (var cur in all)
                {
                    Assert.Equal(ki, KeyNotationUtil.StringToKeyInput(cur));
                }
            }

            [Fact]
            public void HandleCommandKey()
            {
                var ki = KeyNotationUtil.StringToKeyInput("<D-a>");
                Assert.Equal(VimKey.LowerA, ki.Key);
                Assert.Equal(KeyModifiers.Command, ki.KeyModifiers);
            }

            /// <summary>
            /// Make sure we can parse out the nop key
            /// </summary>
            [Fact]
            public void Nop()
            {
                var keyInput = KeyNotationUtil.StringToKeyInput("<nop>");
                Assert.Equal(VimKey.Nop, keyInput.Key);
                Assert.Equal(KeyModifiers.None, keyInput.KeyModifiers);
            }

            /// <summary>
            /// The C-S notation can be abbreviated CS
            /// </summary>
            [Fact]
            public void AlternateShiftAndControlWithNonPrintable()
            {
                Action<string, VimKey> assert = 
                    (name, vimKey) =>
                    {
                        var notation = String.Format("<CS-{0}>", name);
                        var keyInput = KeyNotationUtil.StringToKeyInput(notation);
                        Assert.Equal(vimKey, keyInput.Key);
                        Assert.Equal(KeyModifiers.Shift | KeyModifiers.Control, keyInput.KeyModifiers);
                    };
                assert("Enter", VimKey.Enter);
                assert("F2", VimKey.F2);
            }

            /// <summary>
            /// The CS-A syntax properly ignores the shift when it's applied to an alpha 
            /// </summary>
            [Fact]
            public void AlternateShiftandControlWithAlpha()
            {
                var keyInput = KeyNotationUtil.StringToKeyInput("<CS-A>");
                Assert.Equal(KeyInputUtil.CharWithControlToKeyInput('a'), keyInput);
            }
        }

        public sealed class Many : KeyNotationUtilTest
        {
            [Fact]
            public void TwoAlpha()
            {
                var opt = KeyNotationUtil.TryStringToKeyInputSet("ab");
                Assert.True(opt.IsSome());
                var list = opt.Value.KeyInputs.ToList();
                Assert.Equal(2, list.Count);
                Assert.Equal('a', list[0].Char);
                Assert.Equal('b', list[1].Char);
            }

            [Fact]
            public void InvalidLessThanPrefix()
            {
                var opt = KeyNotationUtil.TryStringToKeyInputSet("<foo");
                Assert.True(opt.IsSome());
                var list = opt.Value.KeyInputs.Select(x => x.Char).ToList();
                Assert.Equal("<foo".ToList(), list);
            }

            [Fact]
            public void NotationThenAlpha()
            {
                var opt = KeyNotationUtil.TryStringToKeyInputSet("<Home>a");
                Assert.True(opt.IsSome());
                var list = opt.Value.KeyInputs.ToList();
                Assert.Equal(2, list.Count);
                Assert.Equal(KeyInputUtil.VimKeyToKeyInput(VimKey.Home), list[0]);
                Assert.Equal('a', list[1].Char);
            }

            [Fact]
            public void TwoNotation()
            {
                var opt = KeyNotationUtil.TryStringToKeyInputSet("<C-x><C-o>");
                Assert.True(opt.IsSome());
                var list = opt.Value.KeyInputs.ToList();
                Assert.Equal(2, list.Count);
                Assert.Equal(KeyInputUtil.CharWithControlToKeyInput('x'), list[0]);
                Assert.Equal(KeyInputUtil.CharWithControlToKeyInput('o'), list[1]);
            }

            /// <summary>
            /// By default the '\' key doesn't have any special meaning in mappings.  It only has escape
            /// properties when the 'B' flag isn't set in cpoptions
            /// </summary>
            [Fact]
            public void EscapeLessThanLiteral()
            {
                AssertMany(@"\<home>", KeyInputSetUtil.OfVimKeyArray(VimKey.Backslash, VimKey.Home));
            }

            [Fact]
            public void LessThanEscapeLiteral()
            {
                AssertMany(@"<lt>lt>", "<lt>");
            }

            [Fact]
            public void AlternateControAndShift()
            {
                var keyInputSet = KeyNotationUtil.StringToKeyInputSet(@"<CS-A><CS-Enter>");
                var list = keyInputSet.KeyInputs.ToList();
                Assert.Equal(KeyInputUtil.CharWithControlToKeyInput('a'), list[0]);
                Assert.Equal(KeyInputUtil.ApplyModifiersToVimKey(VimKey.Enter, KeyModifiers.Control | KeyModifiers.Shift), list[1]);
            }
        }

        public sealed class Misc : KeyNotationUtilTest
        {
            /// <summary>
            /// Case shouldn't matter
            /// </summary>
            [Fact]
            public void StringToKeyInput8()
            {
                var ki = KeyInputUtil.VimKeyToKeyInput(VimKey.Space);
                ki = KeyInputUtil.ChangeKeyModifiersDangerous(ki, KeyModifiers.Shift);
                var all = new string[] { "<S-space>", "<S-SPACE>" };
                foreach (var cur in all)
                {
                    Assert.Equal(ki, KeyNotationUtil.StringToKeyInput(cur));
                }
            }

            [Fact]
            public void SplitIntoKeyNotationEntries1()
            {
                Assert.Equal(
                    new[] { "a", "b" },
                    KeyNotationUtil.SplitIntoKeyNotationEntries("ab"));
            }

            [Fact]
            public void SplitIntoKeyNotationEntries2()
            {
                Assert.Equal(
                    new[] { "<C-j>", "b" },
                    KeyNotationUtil.SplitIntoKeyNotationEntries("<C-j>b"));
            }

            [Fact]
            public void SplitIntoKeyNotationEntries3()
            {
                Assert.Equal(
                    new[] { "<C-J>", "b" },
                    KeyNotationUtil.SplitIntoKeyNotationEntries("<C-J>b"));
            }

            [Fact]
            public void SplitIntoKeyNotationEntries4()
            {
                Assert.Equal(
                    new[] { "<C-J>", "<C-b>" },
                    KeyNotationUtil.SplitIntoKeyNotationEntries("<C-J><C-b>"));
            }

            [Fact]
            public void SplitIntoKeyNotationEntries_InvalidModifierTreatesLessThanLiterally()
            {
                Assert.Equal(
                    new[] { "<", "b", "-", "j", ">" },
                    KeyNotationUtil.SplitIntoKeyNotationEntries("<b-j>"));
            }

            [Fact]
            public void TryStringToKeyInput_BadModifier()
            {
                Assert.True(KeyNotationUtil.TryStringToKeyInput("<b-j>").IsNone());
            }

            [Fact]
            public void TryStringToKeyInputSet_BadModifier()
            {
                var result = KeyNotationUtil.TryStringToKeyInputSet("<b-j>");
                Assert.True(result.IsSome());
                var list = result.Value.KeyInputs.Select(x => x.Char);
                Assert.Equal(new[] { '<', 'b', '-', 'j', '>' }, list);
            }

            [Fact]
            public void BackslasheInRight()
            {
                AssertMany(@"/\v", KeyInputSetUtil.OfVimKeyArray(VimKey.Forwardslash, VimKey.Backslash, VimKey.LowerV));
            }
        }

        public sealed class GetDisplayName : KeyNotationUtilTest
        {
            /// <summary>
            /// When displaying the Control + alpha keys we should be displaying it in the C-X 
            /// format and not the raw character. 
            /// </summary>
            [Fact]
            public void AlphaAndControl()
            {
                foreach (var c in KeyInputUtilTest.CharLettersUpper)
                {
                    var keyInput = KeyInputUtil.CharWithControlToKeyInput(c);

                    // Certain combinations like CTRL-J have a primary key which gets displayed over
                    // them.  Don't test them here
                    if (KeyInputUtil.GetPrimary(keyInput).IsSome())
                    {
                        continue;
                    }

                    var text = String.Format("<C-{0}>", c);
                    Assert.Equal(text, KeyNotationUtil.GetDisplayName(keyInput));
                }
            }

            [Fact]
            public void Alpha()
            {
                foreach (var c in KeyInputUtilTest.CharLettersUpper)
                {
                    var keyInput = KeyInputUtil.CharToKeyInput(c);
                    Assert.Equal(c.ToString(), KeyNotationUtil.GetDisplayName(keyInput));
                }
            }

            [Fact]
            public void AlphaLowerAndAlt()
            {
                foreach (var c in KeyInputUtilTest.CharLettersLower)
                {
                    var keyInput = KeyInputUtil.CharWithAltToKeyInput(c);
                    var shiftedChar = (char)(0x80 | (int)c);
                    Assert.Equal(shiftedChar.ToString(), KeyNotationUtil.GetDisplayName(keyInput));
                }
            }

            [Fact]
            public void NonAlphaWithControl()
            {
                foreach (var c in "()#")
                {
                    var keyInput = KeyInputUtil.CharWithControlToKeyInput(c);
                    var text = String.Format("<C-{0}>", c);
                    Assert.Equal(text, KeyNotationUtil.GetDisplayName(keyInput));
                }
            }
        }
    }
}
