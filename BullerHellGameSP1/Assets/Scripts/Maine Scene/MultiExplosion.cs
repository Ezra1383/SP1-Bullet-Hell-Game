using System.Collections;
using UnityEngine;

namespace BulletHell
{
    public class MultiExplosion : MonoBehaviour
    {
        public int extraCount = 2;
        public float radius = 10f;
        public float maxDelay = 0.2f;
        public float lifetime = 4f;

        [HideInInspector] public bool isSecondary;

        private IEnumerator Start()
        {
            Destroy(gameObject, lifetime);

            if (isSecondary) yield break;

            for (int i = 0; i < extraCount; i++)
            {
                yield return new WaitForSeconds(Random.Range(0.05f, maxDelay));

                Vector3 offset = Random.insideUnitSphere * radius;
                GameObject copy = Instantiate(gameObject, transform.position + offset, transform.rotation);
                copy.GetComponent<MultiExplosion>().isSecondary = true;
            }
        }
    }
}
