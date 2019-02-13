namespace ValidationLibrary
{
    public class GitHubConfiguration
    {
        /// <summary>
        /// Token generated by GitHub (personal user Token)
        /// </summary>
        public string Token { get; set; }

        /// <summary>
        /// Name of the organization that is validated
        /// </summary>
        public string Organization { get; set; }
    }
}