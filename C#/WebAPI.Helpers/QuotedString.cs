namespace WebAPI.Helpers
{
    public class QuotedString
    {
        #region Constants and Fields

        private readonly string text;

        #endregion

        #region Constructors and Destructors

        public QuotedString(string s)
        {
            this.text = Get(s);
        }

        #endregion

        #region Operators

        /// <summary>
        ///   Implicit conversion from string
        /// </summary>
        /// <param name = "s">The string to quote</param>
        /// <returns>A quoted string</returns>
        public static implicit operator QuotedString(string s)
        {
            return new QuotedString(s);
        }

        /// <summary>
        ///   Implicit conversion from QuotedString to string
        /// </summary>
        /// <param name = "qs">The quoted string</param>
        /// <returns>the quoted string text</returns>
        public static implicit operator string(QuotedString qs)
        {
            return qs.text;
        }

        #endregion

        #region Public Methods

        public static string Get(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return text;
            }

            if (text[0] == '"' && text[text.Length - 1] == '"')
            {
                return text;
            }

            return string.Format("\"{0}\"", text);
        }

        public override string ToString()
        {
            return this.text;
        }

        #endregion
    }
}