using System;
using System.Security.Cryptography;
using System.Text;

namespace GAS
{
    public static class GameplayUtilities
    {
        public static int GetHash(string name)
        {
            MD5 md5Hasher = MD5.Create();
            var hashed = md5Hasher.ComputeHash(Encoding.UTF8.GetBytes(name));
            return BitConverter.ToInt32(hashed, 0);
        }

        // public static bool IsEmpty(this GameplayTagContainer tagContainer)
        // {
        //     var tagCount = tagContainer.Tags.Count;
        //     return tagCount <= 0;
        // }

        // public static bool ContainsAnyTag(this GameplayTagContainer tagContainer, GameplayTagContainer checkContainer)
        // {
        //     var checkTags = checkContainer.Tags;
        //     return checkTags.Any(tagContainer.ContainsTag);
        // }

        // public static void AddContainerTagsRuntime(this GameplayTagContainer tagContainer, GameplayTagContainer addingContainer)
        // {
        //     var subTags = addingContainer.Tags;
        //     foreach (var subTag in subTags)
        //     {
        //         tagContainer.AddTagRuntime(subTag);
        //     }
        // }
    }
}
