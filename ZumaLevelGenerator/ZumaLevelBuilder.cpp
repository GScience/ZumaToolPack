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

	while (c != '\r' && c != '\n' && c != EOF)
	{
		line += c;
		c = s.get();

		if (s.eof())
			break;
	}
	return line;
}

bool check_head(const char* head)
{
	return
		head[0] == 0x43 &&
		head[1] == 0x55 &&
		head[2] == 0x52 &&
		head[3] == 0x56 &&
		head[4] == 0x02 &&
		head[5] == 0x00 &&
		head[6] == 0x00 &&
		head[7] == 0x00 &&
		head[8] == 0x01 &&
		head[9] == 0x00 &&
		head[10] == 0x00 &&
		head[11] == 0x00;
}

zumaPath read_binary(const std::string& file)
{
	auto path = zumaPath();

	auto inFile = std::ifstream(file, std::ios::binary);
	char head[12];
	inFile.read(head, sizeof(head));

	if (!check_head(head))
	{
		std::cout << "Header does not match zuma path file" << std::endl;
		return path;
	}

	uint32_t pathInfoSize = 0;
	inFile.read(reinterpret_cast<char*>(&pathInfoSize), sizeof(pathInfoSize));
	inFile.seekg(pathInfoSize, std::ios::cur);

	uint32_t pathLenth = 0;
	inFile.read(reinterpret_cast<char*>(&pathLenth), sizeof(pathLenth));

	point startPoint;

	inFile.read(reinterpret_cast<char*>(&startPoint.pointX), sizeof(startPoint.pointX));
	inFile.read(reinterpret_cast<char*>(&startPoint.pointY), sizeof(startPoint.pointY));
	inFile.read(reinterpret_cast<char*>(&startPoint.can_hit), sizeof(startPoint.can_hit));
	inFile.read(reinterpret_cast<char*>(&startPoint.layer), sizeof(startPoint.layer));

	path.points.emplace_back(startPoint);

	for (auto i = 0u; i < pathLenth - 1; ++i)
	{
		point point;

		int8_t x;
		int8_t y;
		int8_t can_hit;
		int8_t layer;

		inFile.read(reinterpret_cast<char*>(&x), sizeof(x));
		inFile.read(reinterpret_cast<char*>(&y), sizeof(y));
		inFile.read(reinterpret_cast<char*>(&can_hit), sizeof(can_hit));
		inFile.read(reinterpret_cast<char*>(&layer), sizeof(layer));

		point.pointX = x;
		point.pointY = y;
		point.can_hit = can_hit;
		point.layer = layer;

		path.points.emplace_back(point);
	}

	return path;
}

zumaPath read_text(const std::string& file)
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
			std::cout << "[Error] Error line found, line number:" << lineCount << std::endl;
			continue;
		}

		x = (int)args[0];
		y = (int)args[1];
		can_hit = (int)args[2];
		layer = (int)args[3];

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

void create_text(const std::string& file, std::vector<point> pointArray)
{
	auto fs = std::ofstream(file);

	auto isFirstPoint = true;

	for (auto point : pointArray)
	{
		if (isFirstPoint)
		{
			fs << std::to_string(point.pointX) << " "
				<< std::to_string(point.pointY) << " "
				<< std::to_string(point.can_hit) << " "
				<< std::to_string(point.layer) << std::endl;

			isFirstPoint = false;
		}
		else
			fs << std::to_string((int)point.pointX) << " "
			<< std::to_string((int)point.pointY) << " "
			<< std::to_string(point.can_hit) << " "
			<< std::to_string(point.layer) << std::endl;
	}
}

void create_binary(const std::string& file, std::string credit, std::vector<point> pointArray)
{
	auto fs = std::ofstream(file, std::ios::binary);

	// 写入文件头
	fs.write(head, sizeof(head));

	// 使用关键点位置来写版权信息
	credit = std::string(
		"MadeByGScienceStudio(一身正气小完能)&TranslatedByDeathknight") +
		"\tVersion：1.4.0\r" + credit;

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

void ttb(const std::string& inFile, const std::string& outFile)
{
	std::cout << "Loading file" << std::endl;

	auto zumaPath = read_text(inFile);

	std::cout << "Total common waypoints: " << zumaPath.points.size() << std::endl;

	std::cout << "Generate binary files" << std::endl;

	create_binary(outFile, zumaPath.credit, zumaPath.points);
}

void btt(const std::string& inFile, const std::string& outFile)
{
	std::cout << "Loading file" << std::endl;

	auto zumaPath = read_binary(inFile);

	std::cout << "Total common waypoints: " << zumaPath.points.size() << std::endl;

	std::cout << "Generate text file" << std::endl;

	create_text(outFile, zumaPath.points);
}

int main(int argc, char* argv[])
{
	std::cout
		<< "==============" << std::endl
		<< "Made By GScience Studio, Translated by DeathKnight" << std::endl
		<< "==============" << std::endl;

	std::string inFile;
	std::string outFile;

	std::string mode = "ttb";

	if (argc == 1)
	{
		std::cout << "Support command line mode: ZumaLevelGenerator [input file] [output file] <mode>" << std::endl;
		std::cout << "mode:" << std::endl;
		std::cout << "ttb: From text format track to binary track" << std::endl;
		std::cout << "btt: From binary track to text format track" << std::endl;
		std::cout << "Enter file name: eg:[<name>.txt]" << std::endl;
		std::getline(std::cin, inFile);
		std::cout << "Output file name: eg:[<name>.dat]" << std::endl;
		std::getline(std::cin, outFile);

		std::cout << "Mode (default ttb): " << std::endl;

		std::getline(std::cin, mode);

		if (mode != "ttb" && mode != "btt")
			mode = "ttb";
	}
	else if (argc == 3)
	{
		inFile = argv[1];
		outFile = argv[2];
	}
	else if (argc == 4)
	{
		inFile = argv[1];
		outFile = argv[2];
		mode = argv[3];
	}
	else
	{
		std::cout << "Use method: ZumaLevelGenerator [input file] [output file] <b | t>" << std::endl;
		return 0;
	}

	inFile.erase(std::remove(inFile.begin(), inFile.end(), '"'), inFile.end());
	outFile.erase(std::remove(outFile.begin(), outFile.end(), '"'), outFile.end());

	std::cout << inFile.c_str() << "->" << outFile.c_str() << std::endl;

	if (mode == "ttb")
	{
		std::cout << "Current mode: ttb. From txt to binary" << std::endl;

		try
		{
			ttb(inFile, outFile);
		}
		catch (std::exception e)
		{
			std::cout << "An error occurred" << std::endl;
		}
	}
	else if (mode == "btt")
	{
		std::cout << "Current mode: btt. From binary to txt" << std::endl;

		try
		{
			btt(inFile, outFile);
		}
		catch (std::exception e)
		{
			std::cout << "An error occurred" << std::endl;
		}
	}
	else
		std::cout << "Parameter error: unknown parameter " << mode << std::endl;
}