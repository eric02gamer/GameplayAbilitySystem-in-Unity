using System;
using System.Collections.Generic;
using EGF;
using UnityEngine;

namespace GAS
{
    [Serializable]
    public class GameplayTagCountContainer
    {
        [SerializeField]private GameplayTagContainer gameplayTags;
        private readonly Dictionary<int, int> gameplayTagsCount;
        
        public GameplayTagContainer GameplayTagContainer => gameplayTags;
        
        public GameplayTagCountContainer()
        {
            gameplayTags = new GameplayTagContainer();
            gameplayTagsCount = new Dictionary<int, int>();
        }

        public void ClearTags()
        {
            gameplayTags.ClearTagRuntime();
            gameplayTagsCount.Clear();
        }

        private void AddTag(GameplayTagHash tagHash)
        {
            var key = tagHash.GetDictHashInt();
            if (gameplayTagsCount.ContainsKey(key))
                gameplayTagsCount[key] += 1;
            else
            {
                gameplayTagsCount.Add(key, 1);
                gameplayTags.AddTagRuntime(tagHash);
            }
        }

        private void RemoveTag(GameplayTagHash tagHash)
        {
            var key = tagHash.GetDictHashInt();
            if (!gameplayTagsCount.ContainsKey(key)) return;
                
            gameplayTagsCount[key] -= 1;
            if (gameplayTagsCount[key] <= 0)
            {
                gameplayTags.RemoveTagRuntime(tagHash);
                gameplayTagsCount.Remove(key);
            }
        }
        
        public void AddTag(GameplayTagContainer tagContainer)
        {
            void AddNodeTag(GTagRuntimeTrieNode node)
            {
                AddTag(node.hash);
            }
            tagContainer.Traverse(AddNodeTag);
        }

        public void RemoveTag(GameplayTagContainer tagContainer)
        {
            void RemoveNodeTag(GTagRuntimeTrieNode node)
            {
                RemoveTag(node.hash);
            }
            tagContainer.Traverse(RemoveNodeTag);
        }
    }
}
