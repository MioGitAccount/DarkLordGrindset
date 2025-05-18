using Godot;
using System;
using System.Collections.Generic;

public static class PathFindingUtil
{
	private static Dictionary<field, field> cameFrom;

	public static List<field> FindPath(field start, field target)
	{
		cameFrom = new Dictionary<field, field>();

		if (start == target)
			return new List<field> { start };

		Queue<field> queue = new Queue<field>();
		queue.Enqueue(start);
		cameFrom[start] = null;

		while (queue.Count > 0)
		{
			field current = queue.Dequeue();

			if (current == target)
				break;

			foreach (field neighbor in current.Neighbors)
			{
				if (!cameFrom.ContainsKey(neighbor))
				{
					queue.Enqueue(neighbor);
					cameFrom[neighbor] = current;
				}
			}
		}

		return ReconstructPath(start, target);
	}

	private static List<field> ReconstructPath(field start, field target)
	{
		List<field> path = new List<field>();
		field current = target;

		while (current != null)
		{
			path.Add(current);
			current = cameFrom.ContainsKey(current) ? cameFrom[current] : null;
		}

		path.Reverse();
		return path[0] == start ? path : new List<field>();
	}
}
