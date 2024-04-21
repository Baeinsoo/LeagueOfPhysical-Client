using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace LOP
{
    public interface IEntranceComponent
    {
        Task Execute();
    }
}
