#include <fstream>
#include <iostream>
#include <vector>
#include <string>
#include <exception>
#include <algorithm>

const char head[] =
{
	0x43,0x55, 0x52, 0x56, 0x02, 0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00
};

struct point
{
	float pointX, pointY;
	int8_t layer, can_hit;
};

struct zumaPath
{
	std::vector<point> points;
	std::string credit;
};

std::string get_line(std::ifstream& s)
{
	char c = s.get();
	std::string line;

	while (c != '\r' && c != '\n')
	{
		line += c;
		c = s.get();

		if (s.eof())
			break;
	}
	return line;
}

zumaPath read_text(std::string file)
{
	zumaPath zumaPath;

	auto inFile = std::ifstream(file);

	std::vector<point> pointArray;
	std::string credit;

	std::string line;
	line = get_line(inFile);
	int lineCount = 0;

	while (!inFile.eof())
	{
		if (line == "")
		{
			line = get_line(inFile);
			continue;
		}

		point pathPoint;
		int x;
		int y;
		int can_hit;
		int layer;

		size_t index = 0;
		std::vector<double> args;

		if (line[0] == '#')
		{
			credit += (credit == "\t\t" ? "" : "\r\t\t") + line.substr(1);
			line = get_line(inFile);
			continue;
		}

		while (index != std::string::npos)
		{
			auto last = index;
			index = line.find_first_of(" ", index + 1);
			if (index == std::string::npos)
				args.push_back(std::stod(line.substr(last)));
			else
				args.push_back(std::stod(line.substr(last, index - last)));
		}

		++lineCount;

		if (args.size() != 4)
		{
			std::cout << "[错误]发现错误行，行号：" << lineCount << std::endl;
			continue;
		}

		x = (int) args[0];
		y = (int) args[1];
		can_hit = (int) args[2];
		layer = (int) args[3];

		pathPoint.pointX = (float)x;
		pathPoint.pointY = (float)y;
		pathPoint.can_hit = (int8_t)can_hit;
		pathPoint.layer = (int8_t)layer;

		pointArray.push_back(pathPoint);

		line = get_line(inFile);
	}

	zumaPath.points = pointArray;
	zumaPath.credit = credit;
	return zumaPath;
}

void create_file(std::string file, std::string credit, std::vector<point> pointArray)
{
	auto fs = std::ofstream(file, std::ios::binary);

	// 写入文件头
	fs.write(head, sizeof(head));

	// 使用关键点位置来写版权信息
	credit = std::string(
				"\r\r\t===由GScienceStudio(一身正气小完能)制作===\r\r") +
				"\t版本：1.4.0\r\r" + credit;

	while (credit.size() % 10 != 0)
		credit += '\0';

	uint32_t pathInfoBlockSize = sizeof(uint32_t) + credit.size() * sizeof(char);
	uint32_t pathKeypointCount = credit.size() / 10 + 1;

	fs.write(reinterpret_cast<char*>(&pathInfoBlockSize), sizeof(pathInfoBlockSize));
	fs.write(reinterpret_cast<char*>(&pathKeypointCount), sizeof(pathKeypointCount));
	fs.write(reinterpret_cast<char*>(&credit[0]), sizeof(char)* credit.size());

	// 写入路径
	uint32_t pathPointCount = pointArray.size();

	if (pathPointCount == 0)
		throw new std::exception("Point array should be larger than 1");

	fs.write(reinterpret_cast<char*>(&pathPointCount), sizeof(pathPointCount));

	// 开始点
	auto startPoint = pointArray[0];
	fs.write(reinterpret_cast<char*>(&startPoint.pointX), sizeof(startPoint.pointX));
	fs.write(reinterpret_cast<char*>(&startPoint.pointY), sizeof(startPoint.pointY));
	fs.write(reinterpret_cast<char*>(&startPoint.can_hit), sizeof(startPoint.can_hit));
	fs.write(reinterpret_cast<char*>(&startPoint.layer), sizeof(startPoint.layer));

	// 其他点
	for (auto i = 1u; i < pointArray.size(); ++i)
	{
		auto point = pointArray[i];

		int8_t x = (int8_t)point.pointX;
		int8_t y = (int8_t)point.pointY;
		fs.write(reinterpret_cast<char*>(&x), sizeof(x));
		fs.write(reinterpret_cast<char*>(&y), sizeof(y));
		fs.write(reinterpret_cast<char*>(&point.can_hit), sizeof(point.can_hit));
		fs.write(reinterpret_cast<char*>(&point.layer), sizeof(point.layer));
	}
	fs.close();
}

int main(int argc, char* argv[])
{
	std::cout
		<< "==============" << std::endl
		<< "由GScience Studio制作" << std::endl
		<< "==============" << std::endl;

	std::string inFile;
	std::string outFile;

	if (argc == 1)
	{
		std::cout << "支持命令行模式：ZumaLevelGenerator [输入文件] [输出文件]" << std::endl;

		std::cout << "输入文件名" << std::endl;
		std::getline(std::cin, inFile);
		std::cout << "输出文件名" << std::endl;
		std::getline(std::cin, outFile);
	}
	else if (argc == 3)
	{
		inFile = argv[1];
		outFile = argv[2];
	}
	else
	{
		std::cout << "使用方式： ZumaLevelGenerator [输入文件] [输出文件]" << std::endl;
		return 0;
	}
	std::cout << inFile.c_str() << "->" << outFile.c_str() << std::endl;

	inFile.erase(std::remove(inFile.begin(), inFile.end(), '"'), inFile.end());
	outFile.erase(std::remove(outFile.begin(), outFile.end(), '"'), outFile.end());

	std::cout << "加载文件中" << std::endl;

	auto zumaPath = read_text(inFile);

	std::cout << zumaPath.credit << std::endl;
	std::cout << "总共有路径点： " << zumaPath.credit.size() << std::endl;

	std::cout << "生成二进制文件" << std::endl;

	create_file(outFile, zumaPath.credit, zumaPath.points);
}