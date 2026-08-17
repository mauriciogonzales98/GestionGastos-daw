// Sin DisableTestParallelization a propósito: lo que comparte estado es la base MySQL (ADR-002), y
// eso ya lo serializa la colección "base-de-datos" a la que pertenecen todos los tests que la tocan.
// Serializar la suite entera además de eso solo escondía tests que dependían del orden de ejecución.
