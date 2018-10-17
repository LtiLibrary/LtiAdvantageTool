using System.ComponentModel.DataAnnotations;

namespace AdvantageTool.Data
{
    public class Client
    {
        [Required]
        [Display(Name = "ID")]
        public string Id { get; set; }

        [Required]
        [Display(Name = "Name")]
        public string Name { get; set; }

        /// <summary>
        /// The ID of the AdvantagePlatformUser that owns this Client.
        /// </summary>
        public string UserId { get; set; }
    }
}
